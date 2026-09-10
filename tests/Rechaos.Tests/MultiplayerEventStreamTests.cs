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
    /// The schemas are strict on the server, and so is this side: a field the server grew that this
    /// build has never heard of is noticed at the boundary rather than dropped on the floor.
    /// </summary>
    [Fact]
    public async Task RefusesAPayloadCarryingAFieldItDoesNotKnow()
    {
        await Assert.ThrowsAsync<MultiplayerProtocolException>(() => ReadAsync(
            Frame("lobby.hostChanged", "{\"hostPlayerId\":\"p1\",\"smuggled\":true}")));
    }
}
