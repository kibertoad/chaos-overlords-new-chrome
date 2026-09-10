using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilySevenTurnPlannerTests
{
    [Fact]
    public void ArrivingAtResearchSectorInfluencesFirstUsefulSiteAndReplays()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, sourceSites: [4, 2, 8]);
        var player = new PlayerId(0);
        BeginFamilySevenTurn(match, player);
        match.AiPlanning.SetFocusValue(player, 0, AiPlanningState.InactiveFocusValue);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(7, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Influence, command.Action);
        Assert.Equal(CommandTarget.Site(0), command.Target);
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));

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
    public void EstablishedResearchSectorSelectsExactFirstRangedItem()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, sourceSites: [4, 2, 8]);
        var player = new PlayerId(0);
        BeginFamilySevenTurn(match, player);
        match.AiPlanning.SetFocusValue(player, 0, 0);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));
        var expected = OriginalAiFamilySevenRules.SelectFirstResearchItemOfType(
            match, match.Players[0], match.Players[0].Gangs[0], 2);

        Assert.Equal(GangAction.Research, command.Action);
        Assert.Equal(CommandTarget.Item(checked((short)expected!.Value)), command.Target);
        Assert.Equal(expected, match.AiPlanning.FocusValue(player, 0));
    }

    [Fact]
    public void PendingPreviousResearchItemIsRepeated()
    {
        var data = BundledOriginalData.Load();
        var item = checked((byte)data.Items
            .Select((definition, index) => (definition, index))
            .First(value => value.index > 0 && value.definition.Type == 1
                && value.definition.ResearchDifficulty > 0
                && value.definition.TechLevel <= 10).index);
        var match = CreateMatch(data, sourceSites: [4, 2, 8]);
        var player = new PlayerId(0);
        BeginFamilySevenTurn(match, player);
        match.AiPlanning.SetFocusValue(player, 0, 0);
        match.AiPlanning.SetPlannedAction(
            player, 0, GangAction.Research, new AiActionTarget(item, 0));
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Research, command.Action);
        Assert.Equal(CommandTarget.Item(item), command.Target);
    }

    [Fact]
    public void BetterOwnedResearchSectorUsesEncodedOneStepRoute()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, sourceSites: [0, 0, 0],
            researchSector: 18, researchSectorSites: [4, 8, 2]);
        var player = new PlayerId(0);
        BeginFamilySevenTurn(match, player);
        match.AiPlanning.SetFocusValue(player, 0, AiPlanningState.InactiveFocusValue);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(9), command.Target);
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));
    }

    [Fact]
    public void SuccessfulHostileAttackStoresCurrentSectorAsFocus()
    {
        var data = BundledOriginalData.Load();
        var attacker = data.Gangs
            .Where(gang => gang.Stats.Detect >= 10)
            .OrderByDescending(gang => gang.Stats.Combat + gang.Stats.Defense)
            .First();
        var target = data.Gangs.OrderBy(gang => gang.Stats.Combat).First();
        var match = CreateMatch(data, sourceSites: [0, 0, 0],
            attackerDefinitionId: attacker.Id, targetDefinitionId: target.Id,
            sourceOwner: new PlayerId(1), targetSector: 0,
            mentality: AiDifficulty.HomicidalManiac);
        var player = new PlayerId(0);
        BeginFamilySevenTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
        Assert.Equal(0, match.AiPlanning.FocusValue(player, 0));
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void FailedSingleAttackComparisonFallsThroughToResearch()
    {
        var data = BundledOriginalData.Load();
        var pair = (from attackerCandidate in data.Gangs
                    from targetCandidate in data.Gangs
                    where attackerCandidate.Stats.Heal < -3
                        && attackerCandidate.Stats.Detect >= targetCandidate.Stats.Stealth
                        && !OriginalAiFamilySevenRules.CanAttackSelectedTarget(
                            1, attackerCandidate.Stats.Combat, attackerCandidate.Stats.Defense,
                            10, targetCandidate.Stats.Combat, targetCandidate.Stats.Defense)
                    select (Attacker: attackerCandidate, Target: targetCandidate)).First();
        var match = CreateMatch(data, sourceSites: [0, 0, 0], force: 1,
            attackerDefinitionId: pair.Attacker.Id, targetDefinitionId: pair.Target.Id,
            sourceOwner: new PlayerId(1), targetSector: 0,
            mentality: AiDifficulty.HomicidalManiac);
        var player = new PlayerId(0);
        BeginFamilySevenTurn(match, player);
        match.FinishUpkeep();
        var attackerState = match.Players[0].Gangs[0];
        var targetState = match.Players[1].Gangs[0];
        var attackerStats = EffectiveStatisticsCalculator.ForGang(match, attackerState);
        var targetStats = EffectiveStatisticsCalculator.ForGang(match, targetState);
        Assert.False(OriginalAiFamilySevenRules.CanAttackSelectedTarget(
            attackerState.Force, attackerStats.Combat, attackerStats.Defense,
            targetState.Force, targetStats.Combat, targetStats.Defense));

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Research, command.Action);
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void RecoveryHealIsTerminalBeforeResearchSelection()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, sourceSites: [4, 2, 8],
            force: 7, attackerDefinitionId: 0);
        var player = new PlayerId(0);
        BeginFamilySevenTurn(match, player);
        match.AiPlanning.SetFocusValue(player, 0, AiPlanningState.InactiveFocusValue);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));
    }

    [Fact]
    public void ExhaustedResearchChangesToFamilyZeroModeFiveMove()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type != 99)
            .Select(value => checked((short)value.index))
            .ToHashSet();
        var match = CreateMatch(data, sourceSites: [0, 0, 0], researched: researched,
            weaponItemId: 23, armorItemId: 37);
        var player = new PlayerId(0);
        BeginFamilySevenTurn(match, player);
        match.AiPlanning.SetFocusValue(player, 0, 0);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(0, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));
    }

    [Fact]
    public void AvailableUpgradePrecedesResearchAndUsesCostTimesThreeCooldown()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type != 99)
            .Select(value => checked((short)value.index));
        var match = CreateMatch(data, sourceSites: [0, 0, 0],
            cash: 500, researched: researched, attackerDefinitionId: 27,
            hostileNeighbor: true);
        var player = new PlayerId(0);
        BeginFamilySevenTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(data.Items[command.Target.Id].Cost * 3,
            match.AiPlanning.WeaponCooldown(player, 0));
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));
    }

    [Fact]
    public void FinalThreeGreedTurnsOverrideResearchWithTerminate()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, sourceSites: [0, 0, 0],
            scenario: ScenarioId.Greed);
        var player = new PlayerId(0);
        AdvanceCoordinatorToTurn(match.Coordinator, 24, match.Players.Count);
        BeginFamilySevenTurn(match, player);
        match.AiPlanning.SetFocusValue(player, 0, 0);
        match.Coordinator.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Terminate,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    private static void BeginFamilySevenTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 6);
        match.AiPlanning.SetFamily(player, 0, 7);
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
        IReadOnlyList<short> sourceSites,
        int cash = 20,
        int force = 10,
        IEnumerable<short>? researched = null,
        short attackerDefinitionId = 27,
        short targetDefinitionId = 2,
        int targetSector = 63,
        PlayerId? sourceOwner = null,
        int? researchSector = null,
        IReadOnlyList<short>? researchSectorSites = null,
        AiDifficulty mentality = AiDifficulty.Criminal,
        short? weaponItemId = null,
        short? armorItemId = null,
        bool hostileNeighbor = false,
        ScenarioId scenario = ScenarioId.Siege)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], cash,
                [new MatchGangState(new GangId(10), setups[0].Id,
                    attackerDefinitionId, 0, force, weaponItemId, armorItemId)],
                researchedItems: (researched ?? []).ToHashSet()),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id,
                    targetDefinitionId, targetSector, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id =>
            {
                var definitions = id == 0 ? sourceSites
                    : id == researchSector && researchSectorSites is not null
                        ? researchSectorSites
                        : new short[] { 0, 0, 0 };
                return new MatchSectorState(id,
                    Enumerable.Range(0, MatchLimits.SitesPerSector)
                        .Select(slot => new MatchSiteState(
                            slot, definitions[slot],
                            data.Sites.Single(site => site.Id == definitions[slot]).Resistance))
                        .ToArray(),
                    owner: id == 0
                        ? sourceOwner ?? setups[0].Id
                        : id == researchSector ? setups[0].Id
                        : hostileNeighbor && id == 1 ? setups[1].Id : null,
                    income: 0);
            })
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, 41, setups, mentality),
            players, sectors);
    }
}
