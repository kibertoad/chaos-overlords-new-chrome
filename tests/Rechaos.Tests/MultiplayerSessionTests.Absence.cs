using System.Net;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// What the session does about a seat that ran out of time: its own, and somebody else's.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>
    /// A turn that sealed without this client's orders says so.
    /// </summary>
    /// <remarks>
    /// The one fact the interface cannot work out for itself. A turn sealing on the clock takes
    /// whatever draft reached the server, and a draft still in flight when the deadline passed did
    /// not — so "did what I sent count" is answered by the set the turn was resolved from, not by
    /// what this client believes it sent.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ASealedTurnSaysWhetherItCarriedThisSeatsOrders(bool ownOrdersSealed)
    {
        int[] slots = ownOrdersSealed ? [0, 1] : [1];
        var (session, server, http) = Running(configure: fake => fake.Answer(
            HttpMethod.Get, "/turns/1/orders", SealedOrdersForSlots(1, slots)));
        using var _ = http;
        await using var __ = session;

        server.Events.Write(SealedFrameForSlots(8, 1, slots));
        var resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        Assert.Equal(1, resolved.Turn);
        Assert.Equal(ownOrdersSealed, resolved.IncludedOwnOrders);
    }

    /// <summary>
    /// A seat handed to the computer after the match has ended changes nothing, and ends nothing.
    /// </summary>
    /// <remarks>
    /// The narrow case this exists for: a player who runs out of time on the turn that decides the
    /// match is marked absent by that seal, so the vote about them is still on screen over the
    /// endgame, and the match stays running until their report lands. Approving computer control in
    /// that window — one click, in a two-player match — used to throw on every client, because a
    /// finished match is not the clean Command boundary control transfers at, and the throw took
    /// the whole session down and replaced the endgame with a connection failure.
    /// </remarks>
    [Fact]
    public async Task AnApprovedTakeoverAfterTheMatchHasEndedIsIgnoredRatherThanEndingTheSession()
    {
        var finalTurn = TurnTheMatchEndsOn();
        var (session, server, http) = Running(configure: fake =>
        {
            for (var turn = 1; turn <= finalTurn; turn++)
                fake.Answer(HttpMethod.Get, $"/turns/{turn}/orders", SealedOrders(turn));
        });
        using var _ = http;
        await using var __ = session;

        var seq = 8;
        for (var turn = 1; turn <= finalTurn; turn++) server.Events.Write(SealedFrame(seq++, turn));
        var seen = new List<MultiplayerNotice>();
        MultiplayerNotice.TurnResolved resolved;
        do
        {
            resolved = await WaitFor<MultiplayerNotice.TurnResolved>(session, seen);
        }
        while (resolved.Turn < finalTurn);
        Assert.NotNull(resolved.State.Outcome);

        server.Events.Write(Frame(
            seq++, "match.takeoverVoteRequested", $$"""{"playerId":"p2","turn":{{finalTurn}}}"""));
        server.Events.Write(Frame(seq, "match.playerTakenOver", """{"playerId":"p2"}"""));
        var closed = await WaitFor<MultiplayerNotice.TakeoverVoteClosed>(session, seen);

        Assert.True(closed.ComputerControl);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Failed);
        // The seat stays as the finished match recorded it: the endgame everyone is looking at is
        // the state every client confirmed, and a control change after it would only disagree. Read
        // from the session's own copy rather than from the clone the final turn resolved into —
        // that one was taken before the takeover arrived, so it would say Human either way.
        Assert.Equal(PlayerController.Human, session.ControllerOfSlot(1));
    }

    /// <summary>
    /// The turn the time limit ends this harness's match on, found by playing it out locally.
    /// </summary>
    /// <remarks>
    /// The same bootstrap, settings and empty turns the session is about to be fed, so the count is
    /// the one it will reach. It has to be exact: a seal announced for a turn past the end is a
    /// protocol failure, which is the very thing the test would then be unable to tell from the bug.
    /// </remarks>
    private static int TurnTheMatchEndsOn()
    {
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(BundledOriginalData.Load(), Seed, GameSettings, Roster));
        CommandPhase.Enter(replay);
        for (var turn = 1; turn <= ScenarioCatalog.Turns(GameSettings.Duration) + 2; turn++)
        {
            SealedTurnApplier.Apply(replay, SealedOrders(turn));
            if (replay.State.Outcome is not null) return turn;
        }
        throw new InvalidOperationException("the harness match does not reach its time limit");
    }
}
