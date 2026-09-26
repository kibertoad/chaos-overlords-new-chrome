using System.Net;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>How a restore reads a history the server withheld rows from.</summary>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>
    /// The server withholds a stored row its build cannot validate, from the history and the
    /// stream alike, and its log is otherwise gapless: a jump is that row and nothing else.
    /// </summary>
    /// <remarks>
    /// Treating the jump as fatal ended every session of the match for good, since the stream
    /// resynchronises through this replay whenever it sees one.
    /// </remarks>
    [Fact]
    public async Task RestartStepsOverARowTheServerWithheld()
    {
        var view = ViewAtTurn(2) with { LastEventSeq = 4 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            // Sequence 2 was withheld.
            new TurnSealedEvent(
                3, MatchId, "2026-09-10T12:01:00.000Z",
                new(1, SealedOrders(1).OrderSetHash)),
            new TurnOpenedEvent(4, MatchId, "2026-09-10T12:02:00.000Z", new(2, null)),
        ];
        var (session, _, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get,
                    "/snapshots/latest",
                    Envelope("no_snapshot"),
                    HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/2/orders/mine",
                    new OwnSubmissionView(2, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session);

        Assert.Equal(2, resumed.State.Coordinator.Turn);
    }

    /// <summary>
    /// Stepping over a withheld row takes nothing that matters on trust: a withheld seal leaves the
    /// next one naming a turn the reconstructed state has not reached.
    /// </summary>
    [Fact]
    public async Task RestartRefusesAHistoryThatWithheldASeal()
    {
        var view = ViewAtTurn(3) with { LastEventSeq = 3 };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            // Sequence 2, the seal of turn 1, was withheld.
            new TurnSealedEvent(
                3, MatchId, "2026-09-10T12:01:00.000Z",
                new(2, SealedOrders(2).OrderSetHash)),
        ];
        var (session, _, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get,
                    "/snapshots/latest",
                    Envelope("no_snapshot"),
                    HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
            });
        using var _ = http;
        await using var __ = session;

        var failed = await WaitFor<MultiplayerNotice.Failed>(session);

        Assert.Contains("sealed turn 2 while the", failed.Reason, StringComparison.Ordinal);
    }
}
