using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

public sealed class PlayerControlTransferTests
{
    [Fact]
    public void TransferCanReturnToHumanAndUpdatesEverySetupView()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        var before = MatchStateHasher.ComputeFingerprint(match);

        Assert.True(match.TransferPlayerToComputer(new PlayerId(1)));

        Assert.Equal(PlayerController.Computer, match.Setup.Players[1].Controller);
        Assert.Equal(PlayerController.Computer, match.Players[1].Setup.Controller);
        Assert.Equal(match.Setup.Players[1], match.Players[1].Setup);
        Assert.NotEqual(before, MatchStateHasher.ComputeFingerprint(match));
        Assert.False(match.TransferPlayerToComputer(new PlayerId(1)));
        Assert.True(match.TransferPlayerToHuman(new PlayerId(1)));
        Assert.Equal(PlayerController.Human, match.Setup.Players[1].Controller);
        Assert.Equal(PlayerController.Human, match.Players[1].Setup.Controller);
        Assert.Equal(before, MatchStateHasher.ComputeFingerprint(match));
        Assert.False(match.TransferPlayerToHuman(new PlayerId(1)));
    }

    // RULE-AI-001, RULE-AI-027: the computer that takes over a seat plans every gang as a raider
    // from its second pass; on the first the flagged slot takes its hire role's family.
    [Fact]
    public void ATakenOverSeatPlansEveryGangAsARaider()
    {
        var match = CreateMatch();
        var seat = new PlayerId(1);
        match.FinishUpkeep();
        Assert.True(match.TransferPlayerToComputer(seat));
        Assert.True(match.AiPlanning.RaiderMode(seat));

        match.FinishCommand(new PlayerId(0));
        match.PrepareAiPlanning(seat);
        Assert.NotEqual(AiPlanningState.UnusedFamily, match.AiPlanning.Family(seat, 0));

        AdvanceToNextCommandPhase(match);
        match.FinishCommand(new PlayerId(0));
        match.PrepareAiPlanning(seat);

        var active = match.Players[1].Gangs
            .Select((gang, slot) => (gang, slot))
            .Where(entry => entry.gang.IsActive)
            .ToArray();
        Assert.NotEmpty(active);
        Assert.All(active, entry => Assert.Equal(9, match.AiPlanning.Family(seat, entry.slot)));
        Assert.False(match.AiPlanning.RaiderMode(new PlayerId(0)));
    }

    [Fact]
    public void TransferRequiresCleanCommandBoundaryAndKnownSeat()
    {
        var match = CreateMatch();

        Assert.Throws<InvalidOperationException>(() =>
            match.TransferPlayerToComputer(new PlayerId(1)));
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), match.Players[0].Gangs[0].Id,
            GangAction.Hide, Rechaos.Core.GameModel.CommandTarget.None)).Accepted);
        Assert.Throws<InvalidOperationException>(() =>
            match.TransferPlayerToComputer(new PlayerId(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            match.TransferPlayerToComputer(new PlayerId(MatchLimits.PlayerCount)));
    }

    /// <summary>
    /// A carried-over recurring command is not planning in progress.
    /// </summary>
    /// <remarks>
    /// <see cref="TurnCommandQueue.FinishExecution"/> keeps every command with Repeat set, and
    /// Repeat travels in the online order document, so from the first recurring Research or
    /// Influence onward the queue is never empty at a turn boundary. Refusing a transfer on that
    /// threw inside the event pump of every client at once — on an approved takeover, on a player
    /// returning, and on a late join — and the throw is in the match history, so every reconnect
    /// replayed it and the match was lost for good.
    /// </remarks>
    [Fact]
    public void TransferSucceedsWhileAnotherSeatHoldsARecurringCommand()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        var hider = match.Players[0].Gangs[0];
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), hider.Id, GangAction.Hide,
            Rechaos.Core.GameModel.CommandTarget.None, Repeat: true)).Accepted);
        // Resolve the turn so the recurring command is carried into the next Command boundary
        // rather than being one submitted in the turn the transfer is asked about.
        AdvanceToNextCommandPhase(match);
        Assert.NotEmpty(match.Commands.ExecutionPlan());

        Assert.True(match.TransferPlayerToComputer(new PlayerId(1)));

        Assert.Equal(PlayerController.Computer, match.Setup.Players[1].Controller);
        // The recurring command of the seat that did not move is untouched.
        Assert.True(match.Commands.TryGet(hider.Id, out _));
    }

    /// <summary>
    /// The transferred seat's own recurring orders go with the human who left.
    /// </summary>
    /// <remarks>
    /// The AI planner never writes or clears Repeat, so a gang the planner leaves idle would keep
    /// running the departed player's order for the rest of the match.
    /// </remarks>
    [Fact]
    public void TransferToComputerCancelsTheSeatsOwnRecurringCommands()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        var gang = match.Players[1].Gangs[0];
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), gang.Id, GangAction.Hide,
            Rechaos.Core.GameModel.CommandTarget.None, Repeat: true)).Accepted);
        AdvanceToNextCommandPhase(match);
        Assert.True(match.Commands.TryGet(gang.Id, out _));

        Assert.True(match.TransferPlayerToComputer(new PlayerId(1)));

        Assert.False(match.Commands.TryGet(gang.Id, out _));
        Assert.Null(match.FindGang(gang.Id)!.QueuedCommand);
    }

    [Fact]
    public void TransferRejectsASeatWithAuthenticatedHumanComlinkHistory()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        var sent = match.SendComlinkMessage(
            new PlayerId(0), [new PlayerId(1)], "STILL HERE");
        Assert.True(sent.Accepted);

        Assert.Throws<InvalidOperationException>(() =>
            match.TransferPlayerToComputer(new PlayerId(0)));
    }

    [Fact]
    public void TransferSurvivesNativeSaveAndRecordedReplay()
    {
        var definitions = BundledOriginalData.Load();
        var recorder = new MatchReplayRecorder(CreateMatch(definitions));
        recorder.FinishUpkeep();
        Assert.True(recorder.TransferPlayerToComputer(new PlayerId(1)));
        Assert.True(recorder.TransferPlayerToHuman(new PlayerId(1)));

        using var save = new MemoryStream();
        NativeSaveSerializer.Save(save, recorder.State);
        save.Position = 0;
        var restoredSave = NativeSaveSerializer.Load(save, definitions);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restoredReplay = MatchReplaySerializer.LoadAndReplay(replay, definitions);

        Assert.Equal(PlayerController.Human, restoredSave.Setup.Players[1].Controller);
        Assert.Equal(PlayerController.Human, restoredReplay.Setup.Players[1].Controller);
        Assert.Equal(
            MatchStateHasher.ComputeFingerprint(recorder.State),
            MatchStateHasher.ComputeFingerprint(restoredSave));
        Assert.Equal(
            MatchStateHasher.ComputeFingerprint(recorder.State),
            MatchStateHasher.ComputeFingerprint(restoredReplay));
        var step = Assert.Single(recorder.Steps,
            item => item.Kind == ReplayOperationKind.TransferPlayerToComputer);
        Assert.Equal(new PlayerId(1), step.Player);
        Assert.True(step.Accepted);
        var returned = Assert.Single(recorder.Steps,
            item => item.Kind == ReplayOperationKind.TransferPlayerToHuman);
        Assert.Equal(new PlayerId(1), returned.Player);
        Assert.True(returned.Accepted);
    }

    [Fact]
    public void TransferredSeatIsPlannedByEveryLockstepClient()
    {
        var definitions = BundledOriginalData.Load();
        IReadOnlyList<PlayerView> roster =
        [
            new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Active, IsHost: true),
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false)
        ];
        var settings = new MultiplayerGameSettings(
            ScenarioId.Greed, GameDuration.SixMonths,
            AiDifficulty.Criminal, [0, 1, 2, 3, 4, 5]);
        var replay = new MatchReplayRecorder(
            MatchBootstrapFactory.Create(definitions, 1996, settings, roster));
        CommandPhase.Enter(replay);
        Assert.True(replay.TransferPlayerToComputer(new PlayerId(1)));
        var empty = new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []);
        var playerZero = new SealedPlayerOrders(
            "p1", 0, empty, OrderDigest.OfDocument(empty));
        var sealedOrders = new SealedOrdersView(
            1, OrderDigest.OfSet([playerZero]), [playerZero]);

        SealedTurnApplier.Apply(replay, sealedOrders);

        Assert.Contains(replay.Steps, step =>
            step.Kind == ReplayOperationKind.PrepareAiPlanning
            && step.Player == new PlayerId(1));
        using var serialized = new MemoryStream();
        MatchReplaySerializer.Save(serialized, replay);
        serialized.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(serialized, definitions);
        Assert.Equal(
            MatchStateHasher.ComputeFingerprint(replay.State),
            MatchStateHasher.ComputeFingerprint(restored));
    }

    /// <summary>Runs the match on to the Command phase of the next turn, first seat active.</summary>
    private static void AdvanceToNextCommandPhase(MatchState match)
    {
        for (var boundary = 0; boundary < 64; boundary++)
        {
            switch (match.Coordinator.Phase)
            {
                case TurnPhase.Upkeep:
                    match.FinishUpkeep();
                    break;
                case TurnPhase.Command:
                    match.FinishCommand(match.Coordinator.ActivePlayer!.Value);
                    break;
                case TurnPhase.Execution:
                    match.FinishExecutionPhase();
                    break;
                case TurnPhase.Hire:
                    match.FinishHire(match.Coordinator.ActivePlayer!.Value);
                    break;
                default:
                    match.FinishPlayerElimination();
                    break;
            }
            if (match.Coordinator.Phase == TurnPhase.Command
                && match.Coordinator.ActivePlayer == new PlayerId(0))
            {
                return;
            }
        }
        Assert.Fail("The match did not reach the next Command phase.");
    }

    private static MatchState CreateMatch(OriginalData? definitions = null)
    {
        definitions ??= BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ADA", PlayerController.Human),
            new(new PlayerId(1), "GRACE", PlayerController.Human)
        ];
        return OriginalMatchFactory.Create(
            definitions,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players));
    }
}
