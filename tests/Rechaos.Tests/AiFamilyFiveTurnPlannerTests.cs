using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyFiveTurnPlannerTests
{
    [Fact]
    public void FirstContinuationInfluencesHighestSupportSiteAndReplays()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, ownsSource: true);
        var player = new PlayerId(0);
        BeginFamilyFiveTurn(match, player);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(5, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Influence, command.Action);
        Assert.Equal(CommandTarget.Site(2), command.Target);
        Assert.Equal(new AiActionTarget(2, 0),
            match.AiPlanning.PlannedTarget(player, 0));

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void FailedLocalContinuationUsesSupportSiteModeSevenRoute()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, ownsSource: false, ownedSupportSector: 18);
        var player = new PlayerId(0);
        BeginFamilyFiveTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(9), command.Target);
    }

    [Fact]
    public void ModeSevenExcludesSectorWithAnotherPreviousInfluenceAssignment()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, ownsSource: false);
        var player = new PlayerId(0);
        match.Sectors[16].Owner = player;
        match.Sectors[18].Owner = player;
        match.Players[0].AddGang(
            new MatchGangState(new GangId(11), player, 4, 18, 10));
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 5);
        match.AiPlanning.SetFamily(player, 1, 5);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Snitch);
        match.AiPlanning.SetPlannedAction(
            player, 1, GangAction.Influence, new AiActionTarget(0, 0));
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
        match.Coordinator.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = AiTurnPlanner.Plan(match, player)
            .Single(candidate => candidate.Gang == new GangId(10));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(8), command.Target);
    }

    [Fact]
    public void PriorInfluenceRetainsItsUnfinishedSiteBeforeComparingSupport()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, ownsSource: true);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 5);
        match.AiPlanning.SetPlannedAction(
            player, 0, GangAction.Influence, new AiActionTarget(0, 0));
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Influence,
            match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(0, 0),
            match.AiPlanning.PlannedTarget(player, 0));
    }

    [Fact]
    public void OpponentContinuationAttacksVisibleLocalTarget()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, ownsSource: true,
            mentality: AiDifficulty.HomicidalManiac);
        var player = new PlayerId(0);
        match.Players[1].Gangs[0].SectorId = 0;
        SetPreviousAction(match, player, GangAction.Move);
        match.Coordinator.FinishUpkeep();

        Assert.True(match.CanPlayerDetectGang(player, new GangId(20)));
        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Theory]
    [InlineData(ScenarioId.Siege, 11)]
    [InlineData(ScenarioId.Acceptance, 2)]
    public void ThirdConsecutiveMoveChangesToScenarioSpecificFamily(
        ScenarioId scenario,
        int expectedFamily)
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, ownsSource: false,
            ownedSupportSector: 18, scenario: scenario);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 5);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Move);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Move);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Move,
            match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(expectedFamily, match.AiPlanning.Family(player, 0));
    }

    [Fact]
    public void UnhandledPreviousActionPreservesRecoveredNoAction()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, ownsSource: true);
        var player = new PlayerId(0);
        SetPreviousAction(match, player, GangAction.Bribe);
        match.Coordinator.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.None,
            match.AiPlanning.PlannedAction(player, 0));
        Assert.Empty(AiTurnPlanner.Plan(match, player));
    }

    [Fact]
    public void FinalThreeGreedTurnsOverridePreparedActionWithTerminate()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, ownsSource: true, scenario: ScenarioId.Greed);
        var player = new PlayerId(0);
        AdvanceCoordinatorToTurn(match.Coordinator, 24, match.Players.Count);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 5);
        match.Coordinator.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Terminate, command.Action);
        Assert.Equal(GangAction.Terminate,
            match.AiPlanning.PlannedAction(player, 0));
    }

    private static void BeginFamilyFiveTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 2);
    }

    private static void SetPreviousAction(
        MatchState match,
        PlayerId player,
        GangAction action)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 5);
        match.AiPlanning.SetPlannedAction(player, 0, action);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
    }

    private static void AdvanceCoordinatorToTurn(
        TurnCoordinator coordinator,
        int targetTurn,
        int playerCount)
    {
        while (coordinator.Turn < targetTurn)
        {
            coordinator.FinishUpkeep();
            for (var player = 0; player < playerCount; player++)
                coordinator.FinishCommand(new PlayerId(player));
            foreach (var _ in TurnStructure.ExecutionOrder)
                coordinator.FinishExecutionPhase();
            for (var player = 0; player < playerCount; player++)
                coordinator.FinishHire(new PlayerId(player));
            coordinator.FinishPlayerElimination();
        }
    }

    private static MatchState CreateMatch(
        OriginalData data,
        bool ownsSource,
        int? ownedSupportSector = null,
        ScenarioId scenario = ScenarioId.Acceptance,
        AiDifficulty mentality = AiDifficulty.Criminal)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id, 4, 0, 10)],
                researchedItems: new HashSet<short>()),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, 63, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 11, 16),
                new MatchSiteState(2, 5, 15)
            ], owner: ownsSource && id == 0 || id == ownedSupportSector
                ? setups[0].Id
                : null,
                crackdownActive: !ownsSource && id == 0,
                income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, 41, setups, mentality), players, sectors);
    }
}
