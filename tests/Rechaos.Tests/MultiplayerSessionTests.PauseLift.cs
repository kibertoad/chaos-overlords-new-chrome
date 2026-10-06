using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// How a desync pause ends on a client that adopted nothing: the server's
/// <c>match.statusChanged</c> to <c>running</c> reopens planning, and nothing else does.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    private const string Lifted = """{"status":"running"}""";

    private static readonly OwnSubmissionView NothingHeldOnTurnTwo =
        new(2, null, Ready: false, OrdersHash: null);

    /// <summary>
    /// The client that posted the repair already stands on it, so the repair's own
    /// <c>snapshot.available</c> leaves it alone, and the lift is what reopens its planning.
    /// </summary>
    [Fact]
    public async Task TheClientThatPostedTheRepairPlansAgainWhenThePauseLifts()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var ours = await ResolveTurnOneAsync(session, server);
        server.Answer(HttpMethod.Get, "/turns/2/orders/mine", NothingHeldOnTurnTwo);
        server.Events.Write(Frame(9, "turn.desynced", Desync(ours)));
        Assert.True((await WaitFor<MultiplayerNotice.Desynced>(session)).IsRepairing);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 2, "the repair upload");

        server.Events.Write(RepairFrame(10, ours));
        server.Events.Write(Frame(11, "turn.confirmed", $$"""{"turn":1,"stateHash":"{{ours}}"}"""));
        server.Events.Write(Frame(12, "match.statusChanged", Lifted));
        var seen = new List<MultiplayerNotice>();
        var lifted = await WaitFor<MultiplayerNotice.PauseLifted>(session, seen);

        Assert.Equal(2, lifted.Turn);
        Assert.Equal(ours, MatchStateHasher.ComputeFingerprint(lifted.State));
        Assert.NotNull(lifted.Planning);
        Assert.False(lifted.Submission!.Ready);
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Resynced);
    }

    /// <summary>
    /// A client that was waiting and already held the state the repair carries adopts nothing, and
    /// the draft it saved before the pause comes back with planning.
    /// </summary>
    [Fact]
    public async Task AWaitingClientThatHeldTheRepairGetsItsDraftBackWhenThePauseLifts()
    {
        var (session, server, http) = Running(ownPlayerId: "p2", configure: NoSnapshots);
        using var _ = http;
        await using var __ = session;
        var ours = await ResolveTurnOneAsync(session, server);
        var draft = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        server.Answer(
            HttpMethod.Get,
            "/turns/2/orders/mine",
            new OwnSubmissionView(2, draft, Ready: true, OrderDigest.OfDocument(draft)));
        server.Events.Write(Frame(9, "turn.desynced", TiedDesync(ours, "p2", ownReport: ours, "p1")));
        Assert.False((await WaitFor<MultiplayerNotice.Desynced>(session)).IsRepairing);

        server.Events.Write(RepairFrame(10, ours));
        server.Events.Write(Frame(11, "turn.confirmed", $$"""{"turn":1,"stateHash":"{{ours}}"}"""));
        server.Events.Write(Frame(12, "match.statusChanged", Lifted));
        var seen = new List<MultiplayerNotice>();
        var lifted = await WaitFor<MultiplayerNotice.PauseLifted>(session, seen);

        Assert.True(lifted.Submission!.Ready);
        Assert.Equal(OrderDigest.OfDocument(draft), OrderDigest.OfDocument(lifted.Planning!.Build()));
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/snapshots/1"));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Resynced);
    }

    /// <summary>
    /// A client that corrected its own report stays paused: the other seats may still disagree,
    /// and the server refuses orders until it says the match runs again.
    /// </summary>
    [Fact]
    public async Task AClientThatCorrectedItsReportWaitsForTheLiftBeforePlanning()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var correct = await ResolveTurnOneAsync(session, server);
        server.Answer(HttpMethod.Get, "/snapshots/latest", BootstrapSnapshot(session));
        server.Answer(HttpMethod.Get, "/turns/2/orders/mine", NothingHeldOnTurnTwo);

        server.Events.Write(Frame(9, "turn.desynced", TiedDesync(correct, "p1", ownReport: OtherHash, "p1")));
        var seen = new List<MultiplayerNotice>();
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session, seen);

        Assert.False(desynced.IsRepairing);
        Assert.Equal(correct, MatchStateHasher.ComputeFingerprint(desynced.CorrectedState!));

        server.Events.Write(Frame(10, "turn.confirmed", $$"""{"turn":1,"stateHash":"{{correct}}"}"""));
        server.Events.Write(Frame(11, "match.statusChanged", Lifted));
        var lifted = await WaitFor<MultiplayerNotice.PauseLifted>(session, seen);

        Assert.Equal(correct, MatchStateHasher.ComputeFingerprint(lifted.State));
        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.Resynced);
    }

    /// <summary>
    /// A client that adopted a repair it did not hold has already reopened planning with
    /// <see cref="MultiplayerNotice.Resynced"/>, and a match that was never paused on screen has
    /// nothing to reopen; the lift says nothing to either.
    /// </summary>
    [Fact]
    public async Task TheLiftSaysNothingWhenNoPauseIsShowing()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await ResolveTurnOneAsync(session, server);

        server.Events.Write(Frame(9, "match.statusChanged", Lifted));
        server.Events.Write(Readiness(10, turn: 2));
        var seen = new List<MultiplayerNotice>();
        await WaitFor<MultiplayerNotice.ReadinessChanged>(session, seen);

        Assert.DoesNotContain(seen, notice => notice is MultiplayerNotice.PauseLifted);
        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/turns/2/orders/mine"));
    }

    /// <summary>
    /// The interface side: the corrected state is shown with planning still closed, and the lift
    /// reopens planning on the City screen.
    /// </summary>
    [Fact]
    public async Task TheInterfaceLeavesTheDesyncedStageOnlyWhenThePauseLifts()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var correct = await ResolveTurnOneAsync(session, server);
        server.Answer(HttpMethod.Get, "/snapshots/latest", BootstrapSnapshot(session));
        server.Answer(HttpMethod.Get, "/turns/2/orders/mine", NothingHeldOnTurnTwo);
        server.Events.Write(Frame(9, "turn.desynced", TiedDesync(correct, "p1", ownReport: OtherHash, "p1")));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        var game = DeviationBehaviourTests.HeadlessGame();
        DeviationBehaviourTests.Field("_definitions").SetValue(game, BundledOriginalData.Load());
        DeviationBehaviourTests.Field("_session").SetValue(game, session);
        var online = (MultiplayerUiState)DeviationBehaviourTests.Field("_online").GetValue(game)!;
        online.Stage = MultiplayerStage.Playing;
        online.PlanningTurn = 2;

        ApplyNotice(game, desynced);

        Assert.Equal(MultiplayerStage.Desynced, online.Stage);
        Assert.Null(DeviationBehaviourTests.Field("_actions").GetValue(game));
        Assert.Same(desynced.CorrectedState, DeviationBehaviourTests.Field("_state").GetValue(game));

        server.Events.Write(Frame(10, "turn.confirmed", $$"""{"turn":1,"stateHash":"{{correct}}"}"""));
        server.Events.Write(Frame(11, "match.statusChanged", Lifted));
        ApplyNotice(game, await WaitFor<MultiplayerNotice.PauseLifted>(session));

        Assert.Equal(MultiplayerStage.Playing, online.Stage);
        Assert.NotNull(DeviationBehaviourTests.Field("_actions").GetValue(game));
        Assert.Equal(string.Empty, online.TurnSyncError);
    }

    private static void ApplyNotice(ChaosGame game, MultiplayerNotice notice) =>
        (typeof(ChaosGame).GetMethod(
                "Apply",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                [typeof(MultiplayerNotice)])
            ?? throw new MissingMethodException(nameof(ChaosGame), "Apply")).Invoke(game, [notice]);

    private static string RepairFrame(int seq, string stateHash) => Frame(
        seq,
        "snapshot.available",
        $"{{\"turn\":1,\"formatVersion\":{NativeSaveSerializer.CurrentFormatVersion},"
        + $"\"stateHash\":\"{stateHash}\",\"uploadedByPlayerId\":\"p1\"}}");
}
