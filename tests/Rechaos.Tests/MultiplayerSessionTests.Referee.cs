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
/// A match the server referees (docs/MULTIPLAYER.md, "Resolving turns on the server"): the server's
/// state decides each turn, and a client told its report differs adopts the server's snapshot.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    private static string Diverged(string playerId, string stateHash, string reported) =>
        $$"""{"turn":1,"playerId":"{{playerId}}","stateHash":"{{stateHash}}","reportedStateHash":"{{reported}}"}""";

    [Fact]
    public async Task ADivergenceAnnouncedForThisSeatAdoptsTheServerSnapshot()
    {
        var (session, server, http) = Running(matchView: View() with { Refereed = true });
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var ours = await ResolveTurnOneAsync(session, server);

        var served = ServerStateAfterTurnOne();
        server.Answer(HttpMethod.Get, "/snapshots/1", served);
        server.Events.Write(Frame(9, "turn.diverged", Diverged("p1", served.StateHash, ours)));

        var resynced = await WaitFor<MultiplayerNotice.Resynced>(session);
        Assert.Equal(1, resynced.Turn);
        Assert.Equal(served.StateHash, resynced.StateHash);
        // Nothing of its own is posted: the server's snapshot is the state of the turn.
        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/snapshots"));
    }

    [Fact]
    public async Task ADivergenceAnnouncedForAnotherSeatChangesNothingHere()
    {
        var (session, server, http) = Running(matchView: View() with { Refereed = true });
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var ours = await ResolveTurnOneAsync(session, server);
        var seen = new List<MultiplayerNotice>();

        server.Events.Write(Frame(9, "turn.diverged", Diverged("p2", ours, new string('7', 64))));
        // A later seal shows the pump went past the announcement without acting on it.
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));
        server.Events.Write(SealedFrame(10, 2));
        await WaitFor<MultiplayerNotice.TurnResolved>(session, seen);

        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/snapshots/1"));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Resynced);
    }

    /// <summary>
    /// A restart in a refereed match finds a confirmation its own replay does not reach.
    /// </summary>
    /// <remarks>
    /// Without a referee that is a client playing different rules, and the restore ends. With one
    /// the confirmation is the server's own state, so the client is the one that is off, and it
    /// takes the server's snapshot of that turn.
    /// </remarks>
    [Fact]
    public async Task ARestartInARefereedMatchAdoptsTheServerStateItDivergedFrom()
    {
        var served = ServerStateAfterTurnOne();
        Assert.NotEqual(HashAfterTurns(1), served.StateHash);
        var view = ViewAtTurn(2) with { LastEventSeq = 4, Refereed = true };
        MatchEvent[] history =
        [
            new TurnOpenedEvent(1, MatchId, "2026-09-10T12:00:00.000Z", new(1, null)),
            new TurnSealedEvent(
                2, MatchId, "2026-09-10T12:01:00.000Z", new(1, SealedOrders(1).OrderSetHash)),
            new TurnConfirmedEvent(3, MatchId, "2026-09-10T12:01:30.000Z", new(1, served.StateHash)),
            new TurnOpenedEvent(4, MatchId, "2026-09-10T12:02:00.000Z", new(2, null)),
        ];
        var (session, server, http) = Running(
            ownPlayerId: "p2",
            matchView: view,
            configure: fake =>
            {
                fake.Answer(
                    HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
                fake.Answer(HttpMethod.Get, "/snapshots/1", served);
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

        Assert.Equal(served.StateHash, MatchStateHasher.ComputeFingerprint(resumed.State));
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/snapshots/1"));
    }

    /// <summary>
    /// A state after turn 1 that this client's replay does not reach.
    /// </summary>
    /// <remarks>
    /// Every sealed set this harness builds is empty, so no two ways of resolving turn 1 differ.
    /// The server's state stands in for one whose resolution did: turn 1 resolved, with the first
    /// seat holding one more coin than this client's replay leaves it.
    /// </remarks>
    private static SnapshotView ServerStateAfterTurnOne()
    {
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(BundledOriginalData.Load(), Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        SealedTurnApplier.Apply(replay, SealedOrders(1));
        replay.State.Players[0].Cash += 1;
        var hash = MatchStateHasher.ComputeFingerprint(replay.State);
        return new SnapshotView(
            1,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current,
            hash,
            "server",
            "2026-09-10T12:01:30.000Z",
            MatchStateClone.ToBase64(replay.State));
    }
}
