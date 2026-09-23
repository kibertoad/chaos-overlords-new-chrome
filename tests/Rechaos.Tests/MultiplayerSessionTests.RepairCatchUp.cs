using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// A repair for a turn this client has already played past, and the catch-up that follows it.
/// </summary>
/// <remarks>
/// Every test here stands the client on turn 3 when a repair for turn 1 arrives, and answers
/// <c>/snapshots/latest</c> with that repair, as the real server does the moment it is posted. The
/// real server also confirms the disputed turn straight after announcing the repair, which is the
/// event that used to make the repair disappear from any restore that replayed past it.
/// </remarks>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>
    /// A resync asked for while the repair is catching up waits for the adoption rather than
    /// cutting it in half.
    /// </summary>
    [Fact]
    public async Task ResyncDuringRepairCatchUpFinishesTheAdoptionAndResumesOnTheRepairedState()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var repair = await StandOnTurnThreeFacingARepairAsync(session, server);
        server.Answer(HttpMethod.Get, $"/matches/{MatchId}",
            new MatchDetail(ViewAtTurn(3) with { LastEventSeq = 12 }, "CODE1234", "p1"));
        server.Answer(HttpMethod.Get, "/events", new EventPage([Confirmed(12, repair.Snapshot)]));

        var blocked = server.BlockOnce(HttpMethod.Get, "/turns/2/orders");
        server.Events.Write(SnapshotAvailableFrame(11, repair.Snapshot));
        await Until(() => server.CallsTo(HttpMethod.Get, "/turns/2/orders") >= 2,
            "repair catch-up reached the sealed-set read");
        session.RequestResync();
        blocked.SetResult();

        var seen = new List<MultiplayerNotice>();
        var resynced = await WaitFor<MultiplayerNotice.Resynced>(session, seen);
        Assert.Equal(repair.HashAfterCatchUp, resynced.StateHash);
        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session, seen);
        Assert.Equal(3, resumed.State.Coordinator.Turn);
        Assert.Equal(repair.HashAfterCatchUp, MatchStateHasher.ComputeFingerprint(resumed.State));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
        await Until(
            () => server.BodiesSentTo(HttpMethod.Post, "/turns/2/report")
                .Any(body => body.Contains(repair.HashAfterCatchUp, StringComparison.Ordinal)),
            "the repaired turn 2 was reported");
    }

    /// <summary>
    /// A resync asked for while the repair's reports are unanswered is not held up by them.
    /// </summary>
    /// <remarks>
    /// The reporter retries through an outage with no bound. The adoption waits for its reports so
    /// the player is not handed a turn the server still refuses orders for, but a wait that only
    /// the session's end could cut short froze the pump, and every resync the player asked for,
    /// until the server answered.
    /// </remarks>
    [Fact]
    public async Task AResyncWhileTheRepairReportsAreUnansweredIsNotHeldUpByThem()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var repair = await StandOnTurnThreeFacingARepairAsync(session, server);
        server.Answer(HttpMethod.Get, $"/matches/{MatchId}",
            new MatchDetail(ViewAtTurn(3) with { LastEventSeq = 12 }, "CODE1234", "p1"));
        server.Answer(HttpMethod.Get, "/events", new EventPage([Confirmed(12, repair.Snapshot)]));
        var reportsBefore = server.CallsTo(HttpMethod.Post, "/turns/1/report");

        var blocked = server.BlockOnce(HttpMethod.Post, "/turns/1/report");
        server.Events.Write(SnapshotAvailableFrame(11, repair.Snapshot));
        await Until(() => server.CallsTo(HttpMethod.Post, "/turns/1/report") > reportsBefore,
            "the repaired turn's report reached the server");
        session.RequestResync();

        var seen = new List<MultiplayerNotice>();
        var resynced = await WaitFor<MultiplayerNotice.Resynced>(session, seen);
        var resumed = await WaitFor<MultiplayerNotice.Resumed>(session, seen);
        Assert.Equal(repair.HashAfterCatchUp, resynced.StateHash);
        Assert.Equal(repair.HashAfterCatchUp, MatchStateHasher.ComputeFingerprint(resumed.State));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
        blocked.SetResult();
    }

    /// <summary>
    /// A restore that replays the repair and its confirmation out of history still adopts it.
    /// </summary>
    /// <remarks>
    /// A sequence gap after the pause is enough: the <c>snapshot.available</c> and the
    /// <c>turn.confirmed</c> behind it both arrive through the history replay, and the confirmation
    /// used to close the pause without asking whether this client — already past the turn — held
    /// the state it confirmed. It carried on with its own, and its next report desynced the match
    /// again.
    /// </remarks>
    [Fact]
    public async Task ARepairReplayedFromHistoryBehindItsConfirmationIsStillAdopted()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var repair = await StandOnTurnThreeFacingARepairAsync(session, server);
        server.Answer(HttpMethod.Get, $"/matches/{MatchId}",
            new MatchDetail(ViewAtTurn(3) with { LastEventSeq = 12 }, "CODE1234", "p1"));
        server.Answer(HttpMethod.Get, "/events", new EventPage(
        [
            new SnapshotAvailableEvent(11, MatchId, "2026-09-10T12:03:00.000Z", new(
                1, NativeSaveSerializer.CurrentFormatVersion, repair.Snapshot.StateHash, "p2")),
            Confirmed(12, repair.Snapshot),
        ]));

        server.Events.Write(Frame(13, "turn.opened", """{"turn":3,"deadlineAt":null}"""));

        var seen = new List<MultiplayerNotice>();
        var resynced = await WaitFor<MultiplayerNotice.Resynced>(session, seen);
        Assert.Equal(1, resynced.Turn);
        Assert.Equal(repair.HashAfterCatchUp, resynced.StateHash);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
        await Until(
            () => server.BodiesSentTo(HttpMethod.Post, "/turns/1/report")
                .Any(body => body.Contains(repair.Snapshot.StateHash, StringComparison.Ordinal)),
            "the adopted repair was reported");
    }

    /// <summary>
    /// A seat handed to the computer after the last seal is still the computer's after a repair.
    /// </summary>
    /// <remarks>
    /// The handover is a fact of the log, not of any sealed set, and its event is behind the cursor
    /// by the time the repair arrives. A catch-up from sealed sets alone handed the seat back to
    /// its absent human, and this client resolved the next turn without the computer's orders.
    /// </remarks>
    [Fact]
    public async Task ATakeoverBeforeARepairSurvivesTheCatchUp()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var repair = await StandOnTurnThreeFacingARepairAsync(session, server, announce: false);
        var seen = new List<MultiplayerNotice>();

        server.Events.Write(Frame(10, "match.playerTakenOver", """{"playerId":"p2"}"""));
        await WaitFor<MultiplayerNotice.TakeoverVoteClosed>(session, seen);
        Assert.Equal(PlayerController.Computer, session.ControllerOfSlot(1));
        server.Events.Write(Frame(11, "turn.desynced", Desync(repair.Snapshot.StateHash)));
        await WaitFor<MultiplayerNotice.Desynced>(session, seen);
        server.Events.Write(SnapshotAvailableFrame(12, repair.Snapshot));

        var resynced = await WaitFor<MultiplayerNotice.Resynced>(session, seen);
        Assert.Equal(PlayerController.Computer, session.ControllerOfSlot(1));
        Assert.Equal(
            PlayerController.Computer,
            resynced.State.FindPlayer(new PlayerId(1))!.Setup.Controller);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    /// <summary>
    /// A repair that ends the match stops the catch-up there.
    /// </summary>
    /// <remarks>
    /// The server seals the turn after the last one on its deadline when a seat has not reported,
    /// so the turns past a finished repair can include one no finished state can take. Applying it
    /// threw on the pump, and the player was thrown off the endgame into an error modal. The
    /// finished state here stands in for a turn-1 repair: it is a real state with its real hash,
    /// which is all the session checks a repair against.
    /// </remarks>
    [Fact]
    public async Task ARepairThatEndsTheMatchIsAdoptedWithoutApplyingTheTurnsAfterIt()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var finished = FinishedMatchAsATurnOneRepair();
        await StandOnTurnThreeFacingARepairAsync(session, server, repairOverride: finished);
        var seen = new List<MultiplayerNotice>();
        var fetchesBefore = server.CallsTo(HttpMethod.Get, "/turns/2/orders");

        server.Events.Write(SnapshotAvailableFrame(11, finished));

        var resynced = await WaitFor<MultiplayerNotice.Resynced>(session, seen);
        Assert.Equal(finished.StateHash, resynced.StateHash);
        Assert.NotNull(resynced.State.Outcome);
        Assert.Null(resynced.Planning);
        Assert.Equal(fetchesBefore, server.CallsTo(HttpMethod.Get, "/turns/2/orders"));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
    }

    /// <summary>
    /// Every turn the catch-up passes through is reported as that turn left the match.
    /// </summary>
    /// <remarks>
    /// The server finishes a match on the first report that says it is over, and the reports used
    /// to be built only after the whole catch-up, from its final state. The repair here is for
    /// turn 1 and the catch-up runs to the turn the match ends on, so every report before that one
    /// said the match was already finished.
    /// </remarks>
    [Fact]
    public async Task EachCaughtUpTurnIsReportedWithTheStateThatTurnProduced()
    {
        var finalTurn = TurnTheMatchEndsOn();
        var (session, server, http) = Running(configure: fake =>
        {
            for (var turn = 1; turn <= finalTurn; turn++)
                fake.Answer(HttpMethod.Get, $"/turns/{turn}/orders", SealedOrders(turn));
        });
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var seq = 8;
        for (var turn = 1; turn <= finalTurn; turn++) server.Events.Write(SealedFrame(seq++, turn));
        var seen = new List<MultiplayerNotice>();
        MultiplayerNotice.TurnResolved resolved;
        do
        {
            resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session, seen);
        }
        while (resolved.Turn < finalTurn);
        var repair = DivergentRepairForTurnOne(session, catchUpThroughTurn: finalTurn);
        server.Answer(HttpMethod.Get, "/snapshots/latest", repair.Snapshot);
        server.Answer(HttpMethod.Get, "/snapshots/0", BootstrapSnapshot(session));
        server.Answer(HttpMethod.Get, "/snapshots/1", repair.Snapshot);

        server.Events.Write(Frame(seq++, "turn.desynced", Desync(repair.Snapshot.StateHash)));
        server.Events.Write(SnapshotAvailableFrame(seq, repair.Snapshot));

        var resynced = await WaitFor<MultiplayerNotice.Resynced>(session, seen);
        Assert.Equal(repair.HashAfterCatchUp, resynced.StateHash);
        Assert.NotNull(resynced.State.Outcome);
        string LastReport(int turn) => server.BodiesSentTo(HttpMethod.Post, $"/turns/{turn}/report")[^1];
        await Until(
            () => server.BodiesSentTo(HttpMethod.Post, $"/turns/{finalTurn}/report")
                .Any(body => body.Contains(repair.HashAfterCatchUp, StringComparison.Ordinal)),
            "the final caught-up turn was reported");
        for (var turn = 1; turn < finalTurn; turn++)
            Assert.Contains("\"finished\":false", LastReport(turn), StringComparison.Ordinal);
        Assert.Contains("\"finished\":true", LastReport(finalTurn), StringComparison.Ordinal);
    }

    /// <summary>A repair to adopt, and the hash the catch-up on top of it must reach.</summary>
    private sealed record CatchUpRepair(SnapshotView Snapshot, string HashAfterCatchUp);

    /// <summary>
    /// Resolves turns 1 and 2 live, answers the snapshot routes as a server holding a turn-1
    /// repair does, and announces the desync the repair is for at sequence 10.
    /// </summary>
    /// <param name="announce">
    /// False to leave the announcement to the caller, which then owns sequence 10 onwards.
    /// </param>
    private static async Task<CatchUpRepair> StandOnTurnThreeFacingARepairAsync(
        MultiplayerMatchSession session,
        FakeMultiplayerServer server,
        bool announce = true,
        SnapshotView? repairOverride = null)
    {
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot")
            .ConfigureAwait(false);
        await ResolveTurnOneAsync(session, server).ConfigureAwait(false);
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
        server.Events.Write(SealedFrame(9, 2));
        await WaitFor<MultiplayerNotice.TurnResolved>(session).ConfigureAwait(false);
        server.Answer(HttpMethod.Get, "/turns/3/orders/mine",
            new OwnSubmissionView(3, null, Ready: false, OrdersHash: null));

        var repair = repairOverride is null
            ? DivergentRepairForTurnOne(session, catchUpThroughTurn: 2)
            : new CatchUpRepair(repairOverride, repairOverride.StateHash);
        server.Answer(HttpMethod.Get, "/snapshots/latest", repair.Snapshot);
        server.Answer(HttpMethod.Get, "/snapshots/0", BootstrapSnapshot(session));
        server.Answer(HttpMethod.Get, "/snapshots/1", repair.Snapshot);
        if (announce)
        {
            server.Events.Write(Frame(10, "turn.desynced", Desync(repair.Snapshot.StateHash)));
            await WaitFor<MultiplayerNotice.Desynced>(session).ConfigureAwait(false);
        }
        return repair;
    }

    /// <summary>
    /// A valid turn-1 state that is not this client's, and the hash reached by applying the sealed
    /// sets through <paramref name="catchUpThroughTurn"/> on top of it.
    /// </summary>
    private static CatchUpRepair DivergentRepairForTurnOne(
        MultiplayerMatchSession session,
        int catchUpThroughTurn)
    {
        var replay = new MatchReplayRecorder(
            MatchStateClone.Of(session.Bootstrap.State, BundledOriginalData.Load()));
        CommandPhase.Enter(replay);
        SealedTurnApplier.Apply(replay, SealedOrders(1));
        replay.State.Players[0].Cash++;
        // A fresh recorder over the edited state: the old one refuses to go on from a state that
        // changed outside it, and the session's own rebuild starts from a snapshot just like this.
        replay = new MatchReplayRecorder(MatchStateClone.Of(replay.State, BundledOriginalData.Load()));
        var snapshot = RepairSnapshot(replay.State);
        var hash = snapshot.StateHash;
        for (var turn = 2; turn <= catchUpThroughTurn && replay.State.Outcome is null; turn++)
            hash = SealedTurnApplier.Apply(replay, SealedOrders(turn));
        return new CatchUpRepair(snapshot, hash);
    }

    /// <summary>The state the harness match ends in, posted as the repair for turn 1.</summary>
    private static SnapshotView FinishedMatchAsATurnOneRepair()
    {
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(BundledOriginalData.Load(), Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        for (var turn = 1; replay.State.Outcome is null; turn++)
            SealedTurnApplier.Apply(replay, SealedOrders(turn));
        return RepairSnapshot(replay.State);
    }

    private static SnapshotView RepairSnapshot(MatchState state) => new(
        1,
        NativeSaveSerializer.CurrentFormatVersion,
        MultiplayerProtocolVersion.Current,
        MultiplayerSessionVersion.Current,
        MatchStateHasher.ComputeFingerprint(state),
        "p2",
        "2026-09-10T12:03:00.000Z",
        MatchStateClone.ToBase64(state));

    private static string SnapshotAvailableFrame(int seq, SnapshotView repair) => Frame(
        seq,
        "snapshot.available",
        $"{{\"turn\":{repair.Turn},\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion},"
        + $"\"stateHash\":\"{repair.StateHash}\",\"uploadedByPlayerId\":\"p2\"}}");

    /// <summary>The confirmation the server publishes for a repaired turn as soon as it is posted.</summary>
    private static TurnConfirmedEvent Confirmed(int seq, SnapshotView repair) => new(
        seq, MatchId, "2026-09-10T12:03:01.000Z", new(repair.Turn, repair.StateHash));
}
