using System.Net;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// What the session makes of answers the server really gives: a handover the adopted snapshot
/// already holds, a report on the final turn retried after the match finished, a repair another
/// client posted first, and readiness recorded while this client was not listening.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>
    /// A restore replays the log from its start over the newest snapshot, and must not replay the
    /// handovers that snapshot already holds.
    /// </summary>
    /// <remarks>
    /// Ada is voted onto the computer after turn 1 and returns before turn 2; the snapshot after
    /// turn 2 holds her seat as human, with a Comlink message she sent on turn 2. Replaying the old
    /// takeover onto that snapshot tried to hand a seat with human Comlink history to the computer,
    /// which the core refuses, and the reconnect ended in a failure.
    /// </remarks>
    [Fact]
    public async Task RestoreSkipsHandoversTheAdoptedSnapshotAlreadyHolds()
    {
        var definitions = BundledOriginalData.Load();
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        SealedTurnApplier.Apply(replay, SealedOrders(1));
        replay.TransferPlayerToComputer(new PlayerId(0));
        replay.TransferPlayerToHuman(new PlayerId(0));
        Assert.True(replay.State.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "BACK AGAIN").Accepted);
        replay = new MatchReplayRecorder(MatchStateClone.Of(replay.State, definitions));
        SealedTurnApplier.Apply(replay, SealedOrders(2));
        var snapshot = new SnapshotView(
            2,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current,
            MatchStateHasher.ComputeFingerprint(replay.State),
            "p1",
            "2026-09-10T12:04:00.000Z",
            MatchStateClone.ToBase64(replay.State));
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(
                2, MatchId, "2026-09-10T12:01:00.000Z", new(1, SealedOrders(1).OrderSetHash)),
            new MatchPlayerTakenOverEvent(3, MatchId, "2026-09-10T12:01:30.000Z", new("p1")),
            new MatchPlayerReturnedEvent(
                4, MatchId, "2026-09-10T12:01:40.000Z", new("p1", ReplacedComputer: true)),
            new TurnOpenedEvent(5, MatchId, "2026-09-10T12:02:00.000Z", new(2, null)),
            new TurnSealedEvent(
                6, MatchId, "2026-09-10T12:03:00.000Z", new(2, SealedOrders(2).OrderSetHash)),
            new TurnOpenedEvent(7, MatchId, "2026-09-10T12:04:00.000Z", new(3, null)),
        ];
        var (session, server, http) = Running(
            ownPlayerId: "p2",
            matchView: ViewAtTurn(3) with { LastEventSeq = 7 },
            configure: fake =>
            {
                fake.Answer(HttpMethod.Get, "/snapshots/latest", snapshot);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/3/orders/mine",
                    new OwnSubmissionView(3, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;
        var seen = new List<MultiplayerNotice>();

        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session, seen);

        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
        Assert.Equal(PlayerController.Human, resumed.State.Players[0].Setup.Controller);
        Assert.Equal(snapshot.StateHash, MatchStateHasher.ComputeFingerprint(resumed.State));
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/turns/2/orders"));
    }

    /// <summary>
    /// The report that finished the match, retried after its response was lost, is answered
    /// <c>409 match_not_running</c> — the server refuses a match not in progress before it looks at
    /// the turn — and that is the verdict it was asking for, not a failure.
    /// </summary>
    [Fact]
    public async Task ARetriedFinalReportRefusedBecauseTheMatchFinishedIsNotAFailure()
    {
        var finalTurn = TurnTheMatchEndsOn();
        var (session, server, http) = Running(configure: fake =>
        {
            for (var turn = 1; turn <= finalTurn; turn++)
                fake.Answer(HttpMethod.Get, $"/turns/{turn}/orders", SealedOrders(turn));
            fake.AnswerOnce(
                HttpMethod.Post,
                $"/turns/{finalTurn}/report",
                Envelope("match_not_running"),
                HttpStatusCode.Conflict);
        });
        using var _ = http;
        await using var __ = session;

        var seq = 8;
        for (var turn = 1; turn <= finalTurn; turn++) server.Events.Write(SealedFrame(seq++, turn));
        await Until(
            () => server.CallsTo(HttpMethod.Post, $"/turns/{finalTurn}/report") >= 1,
            "the final turn was reported");
        // A later event still reaches the pump, which a failed session would have stopped.
        server.Events.Write(Frame(seq, "turn.readiness", """{"turn":1,"playerId":"p2","ready":true}"""));
        var seen = new List<MultiplayerNotice>();
        await WaitFor<MultiplayerNotice.ReadinessChanged>(session, seen);
        while (session.TryDequeueNotice(out var notice)) seen.Add(notice);

        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
        Assert.Contains(
            "\"finished\":true",
            server.BodiesSentTo(HttpMethod.Post, $"/turns/{finalTurn}/report")[^1],
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The same refusal on a turn that did not end the match still ends the session: the match
    /// stopped while this client still had turns to play.
    /// </summary>
    [Fact]
    public async Task AnEarlierReportRefusedBecauseTheMatchStoppedStillFails()
    {
        var (session, server, http) = Running(configure: fake => fake.AnswerOnce(
            HttpMethod.Post, "/turns/1/report", Envelope("match_not_running"), HttpStatusCode.Conflict));
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));

        server.Events.Write(SealedFrame(8, 1));

        await WaitFor<MultiplayerNotice.Failed>(session);
    }

    /// <summary>
    /// Two clients holding the sole most-reported hash may both post the repair, and the one that
    /// loses the race is refused because the turn no longer needs one. That is not a failure.
    /// </summary>
    [Theory]
    [InlineData("host_only", HttpStatusCode.Forbidden)]
    [InlineData("turn_not_desynced", HttpStatusCode.Conflict)]
    public async Task ARepairRefusedBecauseAnotherLandedFirstKeepsTheSession(
        string reason, HttpStatusCode status)
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var ours = await ResolveTurnOneAsync(session, server);
        server.AnswerOnce(HttpMethod.Post, "/snapshots", Envelope(reason), status);
        var seen = new List<MultiplayerNotice>();

        server.Events.Write(Frame(9, "turn.desynced", Desync(ours)));
        Assert.True((await WaitFor<MultiplayerNotice.Desynced>(session, seen)).IsRepairing);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 2, "the refused upload");
        // The winner's repair settles the pause, and the match carries on.
        server.Events.Write(Frame(10, "turn.confirmed", $$"""{"turn":1,"stateHash":"{{ours}}"}"""));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
        server.Events.Write(SealedFrame(11, 2));
        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session, seen);

        Assert.Equal(2, resolved.Turn);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    /// <summary>
    /// A player who finished the turn before this client started is counted from the view: their
    /// <c>turn.readiness</c> is behind the sequence the stream resumes after.
    /// </summary>
    [Fact]
    public async Task StartTakesReadinessFromTheView()
    {
        var view = View() with
        {
            Turn = View().Turn! with { ReadyPlayerIds = ["p2"] },
        };
        var (session, _, http) = Running(matchView: view);
        using var _ = http;
        await using var __ = session;

        var readiness = await WaitFor<MultiplayerNotice.ReadinessChanged>(session);

        Assert.Equal(1, readiness.Turn);
        Assert.Equal([1], readiness.ReadySlots.Order());
    }

    /// <summary>
    /// A reconnect shows the readiness the table reached while this client was away, rather than
    /// "READY 0/N" until somebody toggles.
    /// </summary>
    [Fact]
    public async Task RestoreTakesReadinessFromTheHistoryAndTheView()
    {
        var baseline = ViewAtTurn(3);
        var view = baseline with
        {
            Turn = baseline.Turn! with { ReadyPlayerIds = ["p2"] },
            LastEventSeq = 11,
        };
        var history = HistoricalEvents(baseline with { LastEventSeq = 10 })
            .Append(new TurnReadinessEvent(
                11, MatchId, "2026-09-10T12:06:00.000Z", new(3, "p2", Ready: true)))
            .ToArray();
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
                fake.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/3/orders/mine",
                    new OwnSubmissionView(3, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;
        var seen = new List<MultiplayerNotice>();

        await WaitFor<MultiplayerNotice.Resumed>(session, seen);
        var readiness = await WaitFor<MultiplayerNotice.ReadinessChanged>(session, seen);

        Assert.Equal(3, readiness.Turn);
        Assert.Equal([1], readiness.ReadySlots.Order());
    }
}
