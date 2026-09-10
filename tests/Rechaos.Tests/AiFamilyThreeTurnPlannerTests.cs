using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyThreeTurnPlannerTests
{
    [Fact]
    public void FirstContinuationInfluencesHighestCashSiteAndReplays()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 4, force: 10, ownsSource: true);
        var player = new PlayerId(0);
        BeginFamilyThreeTurn(match, player);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(3, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Influence, command.Action);
        Assert.Equal(CommandTarget.Site(2), command.Target);
        Assert.Equal(new AiActionTarget(2, 0),
            match.AiPlanning.PlannedTarget(player, 0));

        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void FirstContinuationHealsOnlyBelowRecoveredForceBoundary()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 0, force: 7, ownsSource: true);
        var player = new PlayerId(0);
        BeginFamilyThreeTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void FailedLocalContinuationUsesCashSiteModeEightRoute()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 4, force: 10,
            ownsSource: false, ownedCashSector: 18);
        var player = new PlayerId(0);
        BeginFamilyThreeTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(9), command.Target);
    }

    [Fact]
    public void PriorInfluenceRetainsItsUnfinishedSiteBeforeComparingCash()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 4, force: 10, ownsSource: true);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 3);
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
    public void OpponentContinuationUsesHumanPoolTargetButFullPoolComparisonOrdinal()
    {
        var data = BundledOriginalData.Load();
        var seed = Enumerable.Range(1, 1_000).First(candidate =>
            new DeterministicRandom(candidate).NextInclusive(2) == 2);
        var attacker = data.Gangs
            .OrderByDescending(gang => gang.Stats.Detect)
            .ThenByDescending(gang => gang.Stats.Combat)
            .First();
        var match = CreateOpponentMatch(data, attacker.Id, seed);
        var player = new PlayerId(0);
        BeginFamilyThreeTurn(match, player);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Move);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        Assert.All(match.Players.Skip(1), target =>
            Assert.True(match.CanPlayerDetectGang(player, target.Gangs[0].Id)));
        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(40)), command.Target);
        Assert.Equal(new AiActionTarget(3, 0),
            match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(3, match.Random.ConsumptionCount);

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Theory]
    [InlineData(ScenarioId.Siege, 11)]
    [InlineData(ScenarioId.Power, 2)]
    public void ThirdConsecutiveMoveChangesToScenarioSpecificFamily(
        ScenarioId scenario,
        int expectedFamily)
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 4, force: 10,
            ownsSource: false, ownedCashSector: 18, scenario: scenario);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 3);
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
    public void FinalThreeGreedTurnsOverridePreparedActionWithTerminate()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 4, force: 10, ownsSource: true);
        var player = new PlayerId(0);
        AdvanceCoordinatorToTurn(match.Coordinator, 24, match.Players.Count);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 3);
        match.Coordinator.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Terminate, command.Action);
        Assert.Equal(GangAction.Terminate,
            match.AiPlanning.PlannedAction(player, 0));
    }

    [Fact]
    public void UnhandledPreviousActionPreservesRecoveredNoAction()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, definitionId: 4, force: 10, ownsSource: true);
        var player = new PlayerId(0);
        SetPreviousAction(match, player, GangAction.Bribe);
        match.Coordinator.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.None,
            match.AiPlanning.PlannedAction(player, 0));
        Assert.Empty(AiTurnPlanner.Plan(match, player));
    }

    private static void BeginFamilyThreeTurn(MatchState match, PlayerId player)
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
        match.AiPlanning.SetFamily(player, 0, 3);
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
        short definitionId,
        int force,
        bool ownsSource,
        int? ownedCashSector = null,
        ScenarioId scenario = ScenarioId.Greed)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id, definitionId, 0, force)],
                researchedItems: new HashSet<short>()),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, 63, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 14, 14)
            ], owner: ownsSource && id == 0 || id == ownedCashSector
                ? setups[0].Id
                : null,
                crackdownActive: !ownsSource && id == 0,
                income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, 41, setups), players, sectors);
    }

    private static MatchState CreateOpponentMatch(
        OriginalData data,
        short attackerDefinitionId,
        int seed)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "HUMAN ONE", PlayerController.Human),
            new(new PlayerId(2), "COMPUTER TWO", PlayerController.Computer),
            new(new PlayerId(3), "HUMAN THREE", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id,
                    attackerDefinitionId, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 4, 0, 1)]),
            new(setups[2], 20,
                [new MatchGangState(new GangId(30), setups[2].Id, 4, 0, 1)]),
            new(setups[3], 20,
                [new MatchGangState(new GangId(40), setups[3].Id, 4, 0, 1)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 14, 14)
            ], owner: id == 0 ? setups[1].Id : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, seed, setups,
            AiDifficulty.HomicidalManiac), players, sectors);
    }
}
