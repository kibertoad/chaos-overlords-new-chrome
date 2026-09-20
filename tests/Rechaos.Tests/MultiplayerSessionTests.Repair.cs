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
/// The four ways a desync used to become permanent, and what the session does about each now.
/// </summary>
/// <remarks>
/// Repair was driven entirely by the live <c>turn.desynced</c> event reaching a host that was still
/// standing on the disputed turn — a narrow window on a path that only opens when something has
/// already gone wrong. Everything here left the match paused until retention collected it.
/// </remarks>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>
    /// The divergence is noticed after the next turn has already sealed.
    /// </summary>
    /// <remarks>
    /// One slow seat in a timed match is enough: turn 2 seals on its deadline with that seat still
    /// pending, and the seat's late report for turn 1 disagrees. Every client is on turn 3 by the
    /// time <c>turn.desynced(1)</c> arrives, so a client that offered its CURRENT hash offered one
    /// no candidate list could contain — and the repair simply never happened.
    /// </remarks>
    [Fact]
    public async Task RepairsATurnTheMatchHasAlreadyMovedPast()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
        var afterOne = await ResolveTurnOneAsync(session, server);
        server.Events.Write(SealedFrame(9, 2));
        await WaitFor<MultiplayerNotice.TurnResolved>(session);

        // The bootstrap row is the baseline the rebuild replays from.
        server.Answer(HttpMethod.Get, "/snapshots/latest", BootstrapSnapshot(session));
        server.Events.Write(Frame(10, "turn.desynced", Desync(afterOne)));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        // It rebuilt turn 1's state from the server's own immutable history and posted THAT.
        Assert.True(desynced.IsRepairing);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 2, "the repair upload");
        var upload = server.BodiesSentTo(HttpMethod.Post, "/snapshots")[1];
        Assert.Contains("\"turn\":1", upload, StringComparison.Ordinal);
        Assert.Contains(afterOne, upload, StringComparison.Ordinal);
    }

    /// <summary>
    /// A client restarts during the pause.
    /// </summary>
    /// <remarks>
    /// The <c>turn.desynced</c> that announced it sits behind the view's sequence and is never
    /// re-delivered, so the restored client re-reported the same hash and settled in to wait for a
    /// repair nobody was going to post.
    /// </remarks>
    [Fact]
    public async Task ARestartDuringAPauseRepairsTheDivergenceItFindsInHistory()
    {
        var afterOne = HashAfterTurns(1);
        var view = ViewAtTurn(2) with { LastEventSeq = 3, Status = MatchStatus.Desynced };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(
                2, MatchId, "2026-09-10T12:01:00.000Z", new(1, SealedOrders(1).OrderSetHash)),
            new TurnDesyncedEvent(
                3,
                MatchId,
                "2026-09-10T12:02:00.000Z",
                new(1, [new("p1", afterOne), new("p2", new string('7', 64))], [afterOne])),
        ];
        var (session, server, http) = Running(
            matchView: view,
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                fake.Answer(
                    HttpMethod.Get, "/snapshots/1", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/2/orders/mine",
                    new OwnSubmissionView(2, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        await WaitFor<MultiplayerNotice.Resumed>(session);

        // The pause is picked back up out of history, and this client holds the state the others
        // agreed on, so it posts the repair the restart used to make impossible.
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);
        Assert.Equal(1, desynced.Turn);
        Assert.True(desynced.IsRepairing);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") >= 1, "the repair upload");
        Assert.Contains(
            "\"turn\":1", server.BodiesSentTo(HttpMethod.Post, "/snapshots")[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// A client comes back after the repair was already posted.
    /// </summary>
    /// <remarks>
    /// The restore loaded the snapshot — it is the latest one — but never reported the hash it had
    /// just adopted, so the server went on waiting for a seat that believed it had answered.
    /// </remarks>
    [Fact]
    public async Task ARestartAfterARepairWasPostedAdoptsItAndReportsIt()
    {
        var theirs = HashAfterTurns(1);
        var view = ViewAtTurn(2) with { LastEventSeq = 4, Status = MatchStatus.Desynced };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(
                2, MatchId, "2026-09-10T12:01:00.000Z", new(1, SealedOrders(1).OrderSetHash)),
            new TurnDesyncedEvent(
                3,
                MatchId,
                "2026-09-10T12:02:00.000Z",
                new(1, [new("p1", theirs), new("p2", new string('7', 64))], [theirs])),
            new SnapshotAvailableEvent(
                4,
                MatchId,
                "2026-09-10T12:03:00.000Z",
                new(1, NativeSaveSerializer.CurrentFormatVersion, theirs, "p1")),
        ];
        var (session, server, http) = Running(
            ownPlayerId: "p2",
            matchView: view,
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/events", new EventPage(history));
                fake.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
                fake.Answer(
                    HttpMethod.Get,
                    "/turns/2/orders/mine",
                    new OwnSubmissionView(2, null, Ready: false, OrdersHash: null));
            });
        using var _ = http;
        await using var __ = session;

        await WaitFor<MultiplayerNotice.Resumed>(session);
        await Until(
            () => server.BodiesSentTo(HttpMethod.Post, "/turns/1/report")
                .Any(body => body.Contains(theirs, StringComparison.Ordinal)),
            "the adopted hash was reported");
    }

    /// <summary>
    /// The host is itself the odd one out, which no host-only rule could ever fix.
    /// </summary>
    /// <remarks>
    /// Its own state can never be the majority's, so there was nothing it was allowed to upload —
    /// and the game ended its session over it, which is the moment the match most needed it alive.
    /// It waits now, and adopts the repair a peer posts.
    /// </remarks>
    [Fact]
    public async Task AHostThatIsTheOutlierWaitsForAPeerRepairAndAdoptsIt()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var ours = await ResolveTurnOneAsync(session, server);
        var theirs = new string('7', 64);

        server.Events.Write(Frame(9, "turn.desynced", Desync(theirs)));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        // Nothing to post, and nothing ended: the pause is a wait, not a failure.
        Assert.False(desynced.IsRepairing);
        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/snapshots"));

        // The peer that holds the majority's state posts it, and this host converges on it.
        var repaired = PeerRepair();
        server.Answer(HttpMethod.Get, "/snapshots/1", repaired);
        server.Events.Write(Frame(
            10,
            "snapshot.available",
            $"{{\"turn\":1,\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion},"
            + $"\"stateHash\":\"{repaired.StateHash}\",\"uploadedByPlayerId\":\"p2\"}}"));

        var resynced = await WaitFor<MultiplayerNotice.Resynced>(session);
        Assert.Equal(1, resynced.Turn);
        Assert.NotEqual(ours, resynced.StateHash);
    }

    /// <summary>The bootstrap snapshot as the host itself would have uploaded it.</summary>
    private static SnapshotView BootstrapSnapshot(MultiplayerMatchSession session) => new(
        0,
        NativeSaveSerializer.CurrentFormatVersion,
        MultiplayerProtocolVersion.Current,
        MultiplayerSessionVersion.Current,
        MatchStateHasher.ComputeSha256(session.Bootstrap.State),
        "p1",
        "2026-09-10T12:00:00.000Z",
        MatchStateClone.ToBase64(session.Bootstrap.State));

    /// <summary>
    /// A repair for turn 1 holding a state that is genuinely not this client's.
    /// </summary>
    /// <remarks>
    /// The session verifies a snapshot against the hash it claims and refuses one that does not
    /// hash to its own body, so a fixture cannot label an arbitrary blob as the majority's state:
    /// it has to hand over a real state with its real hash.
    ///
    /// Which state is the awkward part. A desync means two clients computed different states for
    /// the same turn, and nothing in this harness can produce that — every sealed set it builds
    /// carries an EMPTY order document, so every way of resolving turn 1 reaches the same state by
    /// construction. What the test needs is only a state this client is demonstrably not on, and
    /// the state two turns in is one: reachable, deterministic, and different.
    /// </remarks>
    private static SnapshotView PeerRepair()
    {
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(BundledOriginalData.Load(), Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        SealedTurnApplier.Apply(replay, SealedOrders(1));
        var hash = SealedTurnApplier.Apply(replay, SealedOrders(2));
        return new SnapshotView(
            1,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current,
            hash,
            "p2",
            "2026-09-10T12:03:00.000Z",
            MatchStateClone.ToBase64(replay.State));
    }
}
