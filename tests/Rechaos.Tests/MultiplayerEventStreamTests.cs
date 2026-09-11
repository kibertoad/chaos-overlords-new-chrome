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
}
