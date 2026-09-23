using System.Collections.Concurrent;
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
    /// The deadline runs while the parser waits on the body: a keepalive resets it, so a healthy stream
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
    /// Time the consumer spends handling a frame is not silence on the wire.
    /// </summary>
    /// <remarks>
    /// The deadline is paused while the iterator is suspended at <c>yield return</c>, not dropped:
    /// the next read that waits past it still gives up on the connection.
    /// </remarks>
    [Fact]
    public async Task ConsumerWorkLongerThanIdleWindowDoesNotExpireNextRead()
    {
        using var body = new PushStream();
        body.Write(": keepalive\n\n");
        await using var frames = EventStreamParser
            .ReadFramesAsync(body, TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await frames.MoveNextAsync());
        await Task.Delay(250, TestContext.Current.CancellationToken);
        var next = frames.MoveNextAsync().AsTask();
        body.Write(": keepalive\n\n");
        Assert.True(await next);
        Assert.True(frames.Current.IsKeepalive);
        await Assert.ThrowsAsync<EventStreamIdleException>(async () => await frames.MoveNextAsync());
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

    /// <summary>
    /// A bounded retry can end while its last error is still only a failed transport attempt.
    /// An outbox is allowed to begin another window for its idempotent whole-document PUT; a
    /// protocol refusal and a caller-requested cancellation do not take this path.
    /// </summary>
    [Fact]
    public void IdentifiesAnExhaustedTransientRetryForAnIdempotentCaller()
    {
        var exhausted = new RetryExhaustedException(
            attempts: 3,
            elapsed: TimeSpan.FromMinutes(5),
            lastError: new HttpRequestException("connection reset"));

        Assert.True(TransientFailure.CanRetryAfterExhaustion(exhausted));
        Assert.False(TransientFailure.CanRetryAfterExhaustion(
            new RetryExhaustedException(
                attempts: 1,
                elapsed: TimeSpan.Zero,
                lastError: new MultiplayerProtocolException("bad response"))));
    }

    /// <summary>
    /// An EVENT proves a connection at once; a keepalive on its own does not.
    /// </summary>
    /// <remarks>
    /// The first frame of any kind used to be enough, and that made a whole class of broken
    /// middlebox invisible to the backoff — see the one-frame-then-close test below.
    /// </remarks>
    [Fact]
    public async Task ReportsAConnectionOnceAnEventArrivesRatherThanOnAKeepalive()
    {
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        var connected = 0;
        var stream = new MatchEventStream(Handle(http), onConnected: () => connected++);
        await using var read = PendingRead.Start(stream);

        await Until(() => server.CallsTo(HttpMethod.Get, "/stream") == 1);
        Assert.Equal(0, connected);
        server.Events.Write(": keepalive\n\n");
        // A keepalive is what a proxy that accepts and drops also produces, so it proves nothing
        // until the connection has lasted; the event below proves it immediately.
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(0, connected);
        server.Events.Write(Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\"}", "3"));

        Assert.True(await read.Step);
        Assert.Equal(3, read.Current.Seq);
        Assert.Equal(1, connected);
    }

    /// <summary>
    /// A server that accepts, writes a keepalive and drops is an outage, and is backed off from.
    /// </summary>
    /// <remarks>
    /// A proxy with response buffering and a load balancer with a short idle cutoff both do exactly
    /// this. Every attempt used to count as "proven" because a frame had arrived, so the attempt
    /// counter reset to zero and the outage stopwatch never started: the client reconnected at the
    /// first backoff step — half a second to a second — for as long as the player left the match
    /// open, and the retry window that is supposed to end a hopeless reconnect never closed.
    /// </remarks>
    [Fact]
    public async Task BacksOffFromAServerThatAcceptsAndDropsAfterAKeepalive()
    {
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        // The stream reports attempts from its own reconnect loop, which keeps running while the
        // assertions below read them, so the log has to be safe to read while it is written.
        var attempts = new ConcurrentQueue<int>();
        var policy = new RetryPolicy(
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(40),
            MaxAttempts: 0,
            // Long enough that a slow runner still reaches four attempts before the window closes;
            // the loop below stops at four, so the test never waits it out.
            MaxElapsed: TimeSpan.FromSeconds(5));
        var stream = new MatchEventStream(
            Handle(http), policy, onReconnect: (_, attempt) => attempts.Enqueue(attempt));
        await using var read = PendingRead.Start(stream);

        // Accept, write the keepalive a real server opens with, and drop — over and over.
        for (var round = 0; round < 40 && !read.Step.IsCompleted; round++)
        {
            // A closed retry window ends the read, and no further connection will come.
            await Until(() => read.Step.IsCompleted
                || server.CallsTo(HttpMethod.Get, "/stream") >= round + 1);
            if (read.Step.IsCompleted) break;
            server.Events.Write(": keepalive\n\n");
            await Task.Delay(5, TestContext.Current.CancellationToken);
            server.DropStream();
            if (attempts.Count >= 4) break;
        }

        // The attempt counter climbs instead of being reset by each keepalive, so the backoff
        // widens and the outage budget can eventually close. One snapshot, so every assertion
        // judges the same attempts.
        var observed = attempts.ToArray();
        Assert.True(observed.Length >= 4, $"attempts: {string.Join(",", observed)}");
        Assert.Equal(observed.Length, observed.Distinct().Count());
        Assert.Equal(observed.OrderBy(attempt => attempt), observed);
    }

    /// <summary>
    /// An event read together with the <c>MoveNextAsync</c> that is in flight on it.
    /// </summary>
    /// <remarks>
    /// A test that watches how the client reconnects has to leave a step pending while it drives
    /// the server and asserts against what the client did. Disposing an async enumerator while a
    /// step on it has not completed throws <see cref="NotSupportedException"/> from the
    /// compiler-generated <c>DisposeAsync</c>, and a throw from a disposal that runs while the
    /// body is already unwinding <em>replaces</em> the failure being reported: an assertion that
    /// missed on a slow runner arrived as "Specified method is not supported", naming the
    /// disposal and saying nothing about the assertion, which is how a real failure here once
    /// read.
    ///
    /// Holding the token source, the enumerator and the step in one place puts the order beyond
    /// the body's reach -- the step is cancelled and settled first, and only then is the
    /// enumerator disposed -- so a test that fails reports what it was asserting.
    /// </remarks>
    private sealed class PendingRead : IAsyncDisposable
    {
        private readonly IAsyncEnumerator<MatchEvent> events;
        private readonly CancellationTokenSource stop;

        private PendingRead(CancellationTokenSource stop, IAsyncEnumerator<MatchEvent> events)
        {
            this.stop = stop;
            this.events = events;
            // Held as a Task rather than the ValueTask `MoveNextAsync` returns: the body polls it
            // and may await it, and disposal awaits it again to settle it, which a ValueTask does
            // not allow.
            Step = events.MoveNextAsync().AsTask();
        }

        /// <summary>The step started with the read, pollable and awaitable more than once.</summary>
        public Task<bool> Step { get; }

        /// <summary>The event a settled step yielded.</summary>
        public MatchEvent Current => events.Current;

        /// <summary>Starts a read of the whole log and the first step on it.</summary>
        public static PendingRead Start(MatchEventStream stream)
        {
            var stop = new CancellationTokenSource();
            return new PendingRead(
                stop, stream.ReadAsync(0, stop.Token).GetAsyncEnumerator(stop.Token));
        }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            try
            {
                await Step;
            }
            catch
            {
                // However a read ends once nobody is waiting on it -- cancelled, or out of retry
                // budget -- that is not the test's result; the body asserts on whatever outcome it
                // came for. Disposal only has to leave the enumerator safe to dispose, and above
                // all must not throw over the body's own failure.
            }

            await events.DisposeAsync();
            stop.Dispose();
        }
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
