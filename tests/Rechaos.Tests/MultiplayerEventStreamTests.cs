using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The event log arrives as server-sent events, and every frame is a fact the client acts on.
/// A frame read wrongly is worse than one not read at all: it moves the resume cursor.
/// </summary>
public sealed class MultiplayerEventStreamTests
{
    private const string Envelope =
        "\"seq\":3,\"matchId\":\"m1\",\"createdAt\":\"2026-09-10T12:00:00.000Z\"";

    /// <summary>One SSE frame carrying an event of the given type and payload.</summary>
    private static string Frame(string type, string payload, string? id = null) =>
        (id is null ? string.Empty : "id: " + id + "\n")
        + "data: {" + Envelope + ",\"type\":\"" + type + "\",\"payload\":" + payload + "}\n\n";

    /// <summary>
    /// The events a body yields, read through the real entry point.
    /// </summary>
    /// <remarks>
    /// Feeding a stream rather than calling the frame parser directly is what exercises the frame
    /// splitting too — where a blank line ends a frame, and what a body cut mid-frame does.
    /// </remarks>
    private static async Task<List<MatchEvent>> ReadAsync(string body)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
        var events = new List<MatchEvent>();
        await foreach (var @event in EventStreamParser.ReadAsync(stream, CancellationToken.None))
        {
            events.Add(@event);
        }
        return events;
    }

    [Fact]
    public async Task ReadsAnEventAndItsTypedPayload()
    {
        var events = await ReadAsync(Frame(
            "turn.sealed",
            "{\"turn\":7,\"orderSetHash\":\"" + new string('a', 64) + "\"}",
            "3"));

        var sealedTurn = Assert.IsType<TurnSealedEvent>(Assert.Single(events));
        Assert.Equal(3, sealedTurn.Seq);
        Assert.Equal("m1", sealedTurn.MatchId);
        Assert.Equal(7, sealedTurn.Payload.Turn);
    }

    /// <summary>The keepalive is a comment, and a comment is not an event.</summary>
    [Fact]
    public async Task SkipsTheKeepaliveComment()
    {
        var events = await ReadAsync(
            ": keepalive\n\n" + Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\"}"));

        Assert.IsType<LobbyHostChangedEvent>(Assert.Single(events));
    }

    /// <summary>A body cut before a frame's blank line delivers nothing: half an event is not one.</summary>
    [Fact]
    public async Task DropsAFrameThatWasCutMidWrite()
    {
        var complete = Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\"}");
        var truncated = complete + "data: {\"seq\":4,\"matchId\":\"m1\"";

        Assert.Single(await ReadAsync(truncated));
    }

    /// <summary>
    /// The frame id and the payload's own sequence are written from the same number, so a
    /// disagreement means the frame was mangled or the server is not the one this client thinks it
    /// is. Resuming from the wrong number would skip events in silence, so the frame is refused.
    /// </summary>
    [Fact]
    public async Task RefusesAFrameWhoseIdDisagreesWithItsPayload()
    {
        await Assert.ThrowsAsync<MultiplayerProtocolException>(
            () => ReadAsync(Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\"}", "4")));
    }

    /// <summary>
    /// The stream declares one event name, and the server frames every event under it. A frame
    /// named anything else is mangled or foreign, and reading its payload as a match event anyway
    /// would move the resume cursor on something this client cannot claim to understand.
    /// </summary>
    [Fact]
    public async Task RefusesAFrameNamedAnythingButTheContractedEventName()
    {
        var renamed = "event: turn.opened\n"
            + Frame("turn.opened", "{\"turn\":2,\"deadlineAt\":null}");

        await Assert.ThrowsAsync<MultiplayerProtocolException>(() => ReadAsync(renamed));
    }

    /// <summary>The contracted name is read, and a frame that names no event still means it.</summary>
    [Theory]
    [InlineData("event: message\n")]
    [InlineData("")]
    public async Task AcceptsTheContractedEventNameAndItsAbsence(string name)
    {
        var events = await ReadAsync(name + Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\"}"));

        Assert.IsType<LobbyHostChangedEvent>(Assert.Single(events));
    }

    /// <summary>An event this build does not know about is a protocol failure, not a silent skip.</summary>
    [Fact]
    public async Task RefusesAnEventTypeItCannotApply()
    {
        await Assert.ThrowsAsync<MultiplayerProtocolException>(
            () => ReadAsync(Frame("turn.invented", "{}")));
    }

    /// <summary>A multi-line `data:` field is one payload, joined by newlines as the format says.</summary>
    [Fact]
    public async Task JoinsAMultiLineDataField()
    {
        var events = await ReadAsync(
            "data: {" + Envelope + ",\n"
            + "data: \"type\":\"lobby.hostChanged\",\"payload\":{\"hostPlayerId\":\"p1\"}}\n\n");

        Assert.IsType<LobbyHostChangedEvent>(Assert.Single(events));
    }

    /// <summary>
    /// Every event carries the log envelope, because reading a sequence number should not mean
    /// switching over the whole union first.
    /// </summary>
    [Theory]
    [InlineData("lobby.hostChanged", "{\"hostPlayerId\":\"p1\"}")]
    [InlineData("turn.readiness", "{\"turn\":1,\"playerId\":\"p1\",\"ready\":true}")]
    [InlineData("turn.opened", "{\"turn\":2,\"deadlineAt\":null}")]
    [InlineData("match.statusChanged", "{\"status\":\"finished\"}")]
    [InlineData("match.takeoverVoteRequested", "{\"playerId\":\"p2\",\"turn\":1}")]
    [InlineData("match.takeoverVoteCast", "{\"playerId\":\"p2\",\"voterPlayerId\":\"p1\",\"decision\":\"wait\"}")]
    [InlineData("match.takeoverVoteCancelled", "{\"playerId\":\"p2\"}")]
    [InlineData("match.playerTakenOver", "{\"playerId\":\"p2\"}")]
    public async Task CarriesTheEnvelopeOnEveryEventType(string type, string payload)
    {
        var parsed = Assert.Single(await ReadAsync(Frame(type, payload)));

        Assert.Equal(3, parsed.Seq);
        Assert.Equal(type, parsed.Type);
    }

    /// <summary>
    /// A field the server grew is skipped, so a client that predates it keeps playing.
    /// </summary>
    /// <remarks>
    /// The server is deployed separately — self-hosted ones especially — so an additive release has
    /// to be survivable. Refusing the event instead would end the match of every client built before
    /// the field existed, which is a worse outcome than ignoring something this build has no use for.
    /// </remarks>
    [Fact]
    public async Task SkipsAPayloadFieldItDoesNotKnow()
    {
        var events = await ReadAsync(
            Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\",\"smuggled\":true}"));

        var hostChanged = Assert.IsType<LobbyHostChangedEvent>(Assert.Single(events));
        Assert.Equal("p1", hostChanged.Payload.HostPlayerId);
    }

    /// <summary>
    /// Tolerance stops at a null the schema does not allow.
    /// </summary>
    /// <remarks>
    /// Worth pinning next to the test above, because the two look like one setting and are not. A
    /// server that skips a field this build has no use for is a newer server; one that sends null for
    /// a field declared non-nullable is a wrong server, and the difference matters because the second
    /// case would otherwise put a null into a property every caller is entitled to rely on and
    /// surface somewhere else entirely.
    /// </remarks>
    [Fact]
    public async Task RefusesANullWhereThePayloadDeclaresOneCannotBe()
    {
        await Assert.ThrowsAsync<MultiplayerProtocolException>(() => ReadAsync(
            Frame("turn.sealed", "{\"turn\":3,\"orderSetHash\":null}")));
    }

    /// <summary>
    /// A payload read for its digest is held to every field being present.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="WireJson.ReadExact{T}"/>'s promise. Tolerating an absent field
    /// there would leave a default in its place, re-serialize it as that default, and report a digest
    /// mismatch for a set that was never wrong in the way the message implied.
    /// </remarks>
    [Fact]
    public void StillRefusesADigestPayloadMissingAField()
    {
        Assert.Throws<MultiplayerProtocolException>(
            () => WireJson.ReadExact<OrderDocument>("{\"version\":1}"));
    }

    /// <summary>A byte order mark and CRLF line endings are the format's business, not a frame's.</summary>
    [Fact]
    public async Task ToleratesAByteOrderMarkAndCrLf()
    {
        var events = await ReadAsync(
            "\uFEFF" + Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\"}", "3").Replace("\n", "\r\n"));

        Assert.Equal(3, Assert.Single(events).Seq);
    }

    /// <summary>An id that is not a number cannot be resumed from, so the frame is refused.</summary>
    [Fact]
    public async Task RefusesAFrameIdThatIsNotANumber()
    {
        var failure = await Assert.ThrowsAsync<MultiplayerProtocolException>(
            () => ReadAsync(Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\"}", "abc")));

        Assert.Contains("'abc'", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A body that carries nothing for longer than the idle deadline is given up on as dead.
    /// </summary>
    /// <remarks>
    /// The deadline is measured from the last byte: a keepalive resets it, so a healthy stream
    /// between turns is never mistaken for a dead one.
    /// </remarks>
    [Fact]
    public async Task GivesUpOnABodyThatCarriesNothingForTooLong()
    {
        using var body = new PushStream();
        body.Write(": keepalive\n\n");
        await using var frames = EventStreamParser
            .ReadFramesAsync(body, TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await frames.MoveNextAsync());
        Assert.True(frames.Current.IsKeepalive);
        var idle = await Assert.ThrowsAsync<EventStreamIdleException>(async () => await frames.MoveNextAsync());

        Assert.True(TransientFailure.IsTransient(idle));
    }

    /// <summary>
    /// A server that accepts the stream and closes it is not a connection, and reconnecting to it
    /// forever would never surface the failure.
    /// </summary>
    [Fact]
    public async Task ExhaustsTheRetryBudgetWhenTheServerAcceptsAndCloses()
    {
        using var server = new FakeMultiplayerServer { CloseStreamOnOpen = true };
        using var http = new HttpClient(server);
        var connected = 0;
        var stream = new MatchEventStream(
            Handle(http),
            new RetryPolicy(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2), MaxAttempts: 3),
            onConnected: () => connected++);

        await Assert.ThrowsAsync<RetryExhaustedException>(async () =>
        {
            await foreach (var _ in stream.ReadAsync(0, TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Equal(0, connected);
        Assert.Equal(3, server.CallsTo(HttpMethod.Get, "/stream"));
    }

    /// <summary>A connection is reported once its first frame arrives, and a keepalive is a frame.</summary>
    [Fact]
    public async Task ReportsAConnectionOnlyOnceItsFirstFrameArrives()
    {
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        using var stop = new CancellationTokenSource();
        var connected = 0;
        var stream = new MatchEventStream(Handle(http), onConnected: () => connected++);
        await using var events = stream.ReadAsync(0, stop.Token).GetAsyncEnumerator(stop.Token);

        var moving = events.MoveNextAsync();
        await Until(() => server.CallsTo(HttpMethod.Get, "/stream") == 1);
        Assert.Equal(0, connected);
        server.Events.Write(": keepalive\n\n");
        await Until(() => connected == 1);
        server.Events.Write(Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\"}", "3"));

        Assert.True(await moving);
        Assert.Equal(3, events.Current.Seq);
        Assert.Equal(1, connected);
        await stop.CancelAsync();
    }

    private static MatchHandle Handle(HttpClient http) =>
        new MultiplayerClient(http, new MultiplayerClientOptions(new Uri("http://server.test")))
            .WithToken("cop_test")
            .Match("m1");

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("the condition did not hold in time");
            await Task.Delay(15, TestContext.Current.CancellationToken);
        }
    }
}
