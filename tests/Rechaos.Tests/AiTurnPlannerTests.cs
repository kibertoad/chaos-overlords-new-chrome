using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiTurnPlannerTests
{
    [Fact]
    public void PlannerIsDeterministicNonMutatingAndReturnsOnlyLegalCommands()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        var before = MatchStateHasher.ComputeSha256(match);

        var first = AiTurnPlanner.Plan(match, new PlayerId(0));
        var second = AiTurnPlanner.Plan(match, new PlayerId(0));

        Assert.Equal(before, MatchStateHasher.ComputeSha256(match));
        Assert.Equal(first, second);
        Assert.Single(first);
        Assert.All(first, command => Assert.True(CommandValidator.Validate(match, command).IsValid));
        Assert.Equal(first.Count, first.Select(command => command.Gang).Distinct().Count());
    }

    [Fact]
    public void PlannerRejectsHumanAndInactiveTurns()
    {
        var computer = CreateMatch();
        Assert.Throws<InvalidOperationException>(() => AiTurnPlanner.Plan(computer, new PlayerId(0)));

        var human = CreateMatch(PlayerController.Human);
        human.FinishUpkeep();
        Assert.Throws<ArgumentException>(() => AiTurnPlanner.Plan(human, new PlayerId(0)));
    }

    [Fact]
    public void PlannerDoesNotAttackUndetectableGang()
    {
        var data = BundledOriginalData.Load();
        var lowDetect = data.Gangs.OrderBy(gang => gang.Stats.Detect).First();
        var highStealth = data.Gangs.OrderByDescending(gang => gang.Stats.Stealth).First();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), new PlayerId(0), lowDetect.Id, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), new PlayerId(1), highStealth.Id, 0, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ], income: 3))
            .ToArray();
        var match = new MatchState(data,
            new MatchSetup(ScenarioId.KillEmAll, GameDuration.SixMonths, 9, setups), players, sectors);
        match.FinishUpkeep();

        Assert.False(match.CanPlayerDetectGang(new PlayerId(0), new GangId(20)));
        Assert.Contains(CommandOptionCatalog.LegalCommands(match, new PlayerId(0), new GangId(10)),
            command => command.Action == GangAction.Attack);
        Assert.DoesNotContain(AiTurnPlanner.Plan(match, new PlayerId(0)),
            command => command.Action == GangAction.Attack);
    }

    [Fact]
    public void DifficultyChangesAggressionWithoutChangingRulesOrConsumingRandomness()
    {
        var goon = CreateMatch(difficulty: AiDifficulty.Goon);
        var crimeLord = CreateMatch(difficulty: AiDifficulty.CrimeLord);
        goon.FinishUpkeep();
        crimeLord.FinishUpkeep();
        var goonConsumption = goon.Random.ConsumptionCount;
        var crimeLordConsumption = crimeLord.Random.ConsumptionCount;

        var goonPlan = AiTurnPlanner.Plan(goon, new PlayerId(0));
        var crimeLordPlan = AiTurnPlanner.Plan(crimeLord, new PlayerId(0));

        Assert.True(AiTurnPlanner.DifficultyAttackBias(AiDifficulty.Goon)
            < AiTurnPlanner.DifficultyAttackBias(AiDifficulty.Criminal));
        Assert.True(AiTurnPlanner.DifficultyAttackBias(AiDifficulty.Criminal)
            < AiTurnPlanner.DifficultyAttackBias(AiDifficulty.CrimeLord));
        Assert.True(AiTurnPlanner.DifficultyAttackBias(AiDifficulty.CrimeLord)
            < AiTurnPlanner.DifficultyAttackBias(AiDifficulty.HomicidalManiac));
        Assert.All(goonPlan.Concat(crimeLordPlan), command =>
            Assert.True(CommandValidator.Validate(
                command.Player == new PlayerId(0) && goonPlan.Contains(command) ? goon : crimeLord,
                command).IsValid));
        Assert.Equal(goonConsumption, goon.Random.ConsumptionCount);
        Assert.Equal(crimeLordConsumption, crimeLord.Random.ConsumptionCount);
    }

    [Fact]
    public void PlannerRequiresStrictSoloControlAdvantageAtOriginalBoundary()
    {
        var data = BundledOriginalData.Load();
        var definition = data.Gangs.First(candidate =>
            ManualRules.MinimumSectorIncome - candidate.Stats.Control is >= 1 and < ManualRules.MaximumForce);
        var equalForce = ManualRules.MinimumSectorIncome - definition.Stats.Control;
        var equal = CreateNeutralControlMatch(data, definition.Id, equalForce);
        var advantage = CreateNeutralControlMatch(data, definition.Id, equalForce + 1);
        equal.FinishUpkeep();
        advantage.FinishUpkeep();

        var equalGang = equal.FindGang(new GangId(10))!;
        var advantageGang = advantage.FindGang(new GangId(10))!;
        Assert.False(AiTurnPlanner.CanSoloControl(equal, new PlayerId(0), equalGang));
        Assert.True(AiTurnPlanner.CanSoloControl(advantage, new PlayerId(0), advantageGang));
        Assert.DoesNotContain(AiTurnPlanner.Plan(equal, new PlayerId(0)),
            command => command.Action == GangAction.Control);
        Assert.Contains(AiTurnPlanner.Plan(advantage, new PlayerId(0)),
            command => command.Action == GangAction.Control);
    }

    [Fact]
    public void SoloControlEstimateDoesNotCountUndetectableDefenders()
    {
        var data = BundledOriginalData.Load();
        var attackerDefinition = data.Gangs.OrderBy(candidate => candidate.Stats.Detect).First();
        var defenderDefinition = data.Gangs.OrderByDescending(candidate =>
            candidate.Stats.Stealth + candidate.Stats.Control).First();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), new PlayerId(0), attackerDefinition.Id, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), new PlayerId(1), defenderDefinition.Id, 0, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ], owner: id == 0 ? new PlayerId(1) : null,
                income: ManualRules.MinimumSectorIncome))
            .ToArray();
        var match = new MatchState(data, new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, 23, setups), players, sectors);
        var attacker = match.FindGang(new GangId(10))!;

        Assert.False(match.CanPlayerDetectGang(new PlayerId(0), new GangId(20)));
        Assert.True(AiTurnPlanner.CanSoloControl(match, new PlayerId(0), attacker));
    }

    [Fact]
    public void PlannerUsesOriginalHealForceAndSkillBoundaries()
    {
        var data = BundledOriginalData.Load();
        var capable = data.Gangs.First(candidate => candidate.Stats.Heal >= -3).Id;
        var incapable = data.Gangs.First(candidate => candidate.Stats.Heal < -3).Id;
        var forceEight = CreateMatch(definitionId: capable, force: 8);
        var forceNine = CreateMatch(definitionId: capable, force: 9);
        var noHealSkill = CreateMatch(definitionId: incapable, force: 8);
        forceEight.FinishUpkeep();
        forceNine.FinishUpkeep();
        noHealSkill.FinishUpkeep();

        Assert.Contains(AiTurnPlanner.Plan(forceEight, new PlayerId(0)),
            command => command.Action == GangAction.Heal);
        Assert.DoesNotContain(AiTurnPlanner.Plan(forceNine, new PlayerId(0)),
            command => command.Action == GangAction.Heal);
        Assert.DoesNotContain(AiTurnPlanner.Plan(noHealSkill, new PlayerId(0)),
            command => command.Action == GangAction.Heal);
    }

    [Fact]
    public void FamilyOneNoActionBranchDrivesLiveHealCrackdownAndOlderSnitchChoices()
    {
        var data = BundledOriginalData.Load();
        var capable = data.Gangs.First(candidate => candidate.Stats.Heal >= -3).Id;
        var heal = CreateMatch(definitionId: capable, force: 7, data: data);
        var crackdown = CreateMatch(definitionId: capable, force: 7, data: data);
        var priorSnitch = CreateMatch(definitionId: capable, force: 8, data: data);
        foreach (var match in new[] { heal, crackdown, priorSnitch })
        {
            match.FinishUpkeep();
            match.AiPlanning.SetFamily(new PlayerId(0), 0, 1);
        }
        crackdown.Sectors[0].CrackdownActive = true;
        priorSnitch.AiPlanning.SetPlannedAction(new PlayerId(0), 0, GangAction.Snitch);
        priorSnitch.AiPlanning.RollActiveGangActions(
            new PlayerId(0), priorSnitch.Players[0].Gangs);
        priorSnitch.AiPlanning.SetPlannedAction(new PlayerId(0), 0, GangAction.None);
        priorSnitch.AiPlanning.RollActiveGangActions(
            new PlayerId(0), priorSnitch.Players[0].Gangs);

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(heal, new PlayerId(0))).Action);
        Assert.Equal(GangAction.Move,
            Assert.Single(AiTurnPlanner.Plan(crackdown, new PlayerId(0))).Action);
        Assert.Equal(GangAction.Chaos,
            Assert.Single(AiTurnPlanner.Plan(priorSnitch, new PlayerId(0))).Action);
    }

    [Fact]
    public void FamilyOneHealContinuationDrivesLiveRepeatControlAndMoveChoices()
    {
        var data = BundledOriginalData.Load();
        var capable = data.Gangs.First(candidate => candidate.Stats.Heal >= -3).Id;
        var repeatHeal = CreateMatch(definitionId: capable, force: 8, data: data);
        var takeControl = CreateNeutralControlMatch(data, capable, force: 10);
        var move = CreateMatch(definitionId: capable, force: 10, data: data);
        foreach (var match in new[] { repeatHeal, takeControl, move })
        {
            match.FinishUpkeep();
            match.AiPlanning.SetFamily(new PlayerId(0), 0, 1);
            match.AiPlanning.SetPlannedAction(new PlayerId(0), 0, GangAction.Heal);
            match.AiPlanning.RollActiveGangActions(
                new PlayerId(0), match.Players[0].Gangs);
        }

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(repeatHeal, new PlayerId(0))).Action);
        Assert.Equal(GangAction.Control,
            Assert.Single(AiTurnPlanner.Plan(takeControl, new PlayerId(0))).Action);
        Assert.Equal(GangAction.Move,
            Assert.Single(AiTurnPlanner.Plan(move, new PlayerId(0))).Action);
    }

    [Fact]
    public void FamilyOnePostEquipmentContinuationRunsWhenSelectorSixCIsClear()
    {
        var match = CreateMatch(
            scenario: ScenarioId.Greed,
            ownsStartingSector: false,
            cash: 50);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 1);
        match.AiPlanning.SetCurrentHireRole(player, 5);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Control);
        match.Sectors[0].Owner = new PlayerId(1);
        match.FinishUpkeep();
        match.Sectors[0].Tolerance = 3;

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(1, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Control, match.AiPlanning.PreviousAction(player, 0));
        Assert.Equal(GangAction.Chaos, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(GangAction.Chaos, command.Action);
    }

    [Fact]
    public void FamilyOneEquipmentGatePlansExactWeaponAndStartsCooldown()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type != 99)
            .Select(value => checked((short)value.index))
            .ToHashSet();
        var match = CreateMatch(
            definitionId: 56,
            scenario: ScenarioId.Power,
            ownsStartingSector: false,
            data: data,
            cash: 100,
            researchedItems: researched);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 1);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Control);
        match.Sectors[1].Owner = new PlayerId(1);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        var expectedItem = Assert.IsType<int>(
            OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
                match, match.Players[0], match.Players[0].Gangs[0], match.Players[0].Cash));
        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(checked((byte)expectedItem), 0),
            match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(OriginalAiEquipmentRules.EquipmentReplacementCooldown(
                data.Items[expectedItem].Cost),
            match.AiPlanning.WeaponCooldown(player, 0));
        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(expectedItem), command.Target);

        var cashBefore = match.Players[0].Cash;
        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        Assert.Equal(checked((short)expectedItem), match.Players[0].Gangs[0].WeaponItemId);
        Assert.Equal(cashBefore - data.Items[expectedItem].Cost, match.Players[0].Cash);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match), MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(checked((short)expectedItem), restored.Players[0].Gangs[0].WeaponItemId);
        Assert.Equal(match.AiPlanning.WeaponCooldown(player, 0),
            restored.AiPlanning.WeaponCooldown(player, 0));
    }

    [Fact]
    public void FamilyOneEquipmentGatePlansAndResolvesExactArmorUpgrade()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type != 99)
            .Select(value => checked((short)value.index))
            .ToHashSet();
        var match = CreateMatch(
            definitionId: 56,
            scenario: ScenarioId.Power,
            ownsStartingSector: false,
            data: data,
            cash: 100,
            researchedItems: researched);
        var player = new PlayerId(0);
        match.Players[0].Gangs[0].WeaponItemId = 23;
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 1);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Control);
        match.Sectors[1].Owner = new PlayerId(1);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        var expectedItem = Assert.IsType<int>(OriginalAiEquipmentRules.SelectArmorUpgrade(
            match, match.Players[0], match.Players[0].Gangs[0], match.Players[0].Cash));
        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(checked((byte)expectedItem), 0),
            match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(OriginalAiEquipmentRules.EquipmentReplacementCooldown(
                data.Items[expectedItem].Cost),
            match.AiPlanning.ArmorCooldown(player, 0));
        Assert.Equal(CommandTarget.Item(expectedItem), command.Target);

        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        Assert.Equal(checked((short)expectedItem), match.Players[0].Gangs[0].ArmorItemId);
        Assert.Equal((short)23, match.Players[0].Gangs[0].WeaponItemId);
    }

    [Theory]
    [InlineData(ScenarioId.BigMan, 1, 13, 0, 9)]
    [InlineData(ScenarioId.Eliminate, 2, 14, 20, 12)]
    public void ObjectiveFamilyTerminalMoveIsPreparedAndResolved(
        ScenarioId scenario,
        int hireRole,
        int expectedFamily,
        int startingSector,
        int expectedDestination)
    {
        var match = CreateMatch(
            scenario: scenario,
            ownsStartingSector: false,
            startingSector: startingSector);
        var player = new PlayerId(0);
        match.AiPlanning.SetCurrentHireRole(player, hireRole);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(expectedFamily, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Move, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(checked((byte)expectedDestination), 0),
            match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(expectedDestination), command.Target);

        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        Assert.Equal(expectedDestination, match.Players[0].Gangs[0].SectorId);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, match.Definitions);
        Assert.Equal(MatchStateHasher.ComputeSha256(match), MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(expectedDestination, restored.Players[0].Gangs[0].SectorId);
    }

    [Fact]
    public void FamilyFourteenOnObjectiveControlContinuationHealsAndBecomesFamilyThirteen()
    {
        var data = BundledOriginalData.Load();
        var capable = data.Gangs.First(candidate => candidate.Stats.Heal >= -3).Id;
        var match = CreateMatch(
            definitionId: capable,
            force: 9,
            scenario: ScenarioId.BigMan,
            startingSector: 27,
            data: data);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 2);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Control);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Control, match.AiPlanning.PreviousAction(player, 0));
        Assert.Equal(13, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Heal, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(GangAction.Heal, command.Action);

        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match), MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(13, restored.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Heal, restored.AiPlanning.PlannedAction(player, 0));
    }

    [Theory]
    [InlineData(ScenarioId.BigMan, 27)]
    [InlineData(ScenarioId.Eliminate, 9)]
    public void FamilyThirteenOwnedObjectiveWithoutVisibleOpponentHealsAndReplays(
        ScenarioId scenario,
        int objectiveSector)
    {
        var data = BundledOriginalData.Load();
        var capable = data.Gangs.First(candidate => candidate.Stats.Heal >= -3).Id;
        var match = CreateMatch(
            definitionId: capable,
            force: 9,
            scenario: scenario,
            startingSector: objectiveSector,
            data: data);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 1);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(13, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Heal, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(GangAction.Heal, command.Action);

        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match), MatchStateHasher.ComputeSha256(restored));
        Assert.Equal(GangAction.Heal, restored.AiPlanning.PlannedAction(player, 0));
    }

    [Fact]
    public void FamilyOnePreparationUsesRecoveredModeFiveMoveDestination()
    {
        var data = BundledOriginalData.Load();
        var controller = data.Gangs.MaxBy(candidate => candidate.Stats.Control)!.Id;
        var match = CreateMatch(definitionId: controller, force: 8, data: data);
        var player = new PlayerId(0);
        match.AiPlanning.SetCurrentHireRole(player, 1);
        match.Sectors[8].CrackdownActive = true;
        match.Sectors[9].CrackdownActive = true;
        match.FinishUpkeep();
        var consumptionBefore = match.Random.ConsumptionCount;

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(1, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Move, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(1, 0), match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(1), command.Target);
        Assert.Equal(consumptionBefore, match.Random.ConsumptionCount);
    }

    [Fact]
    public void ModeFiveTieConsumesOneBoundedDrawDuringReplayablePreparationOnly()
    {
        var data = BundledOriginalData.Load();
        var controller = data.Gangs.MaxBy(candidate => candidate.Stats.Control)!.Id;
        var match = CreateMatch(definitionId: controller, force: 8, data: data);
        var player = new PlayerId(0);
        match.AiPlanning.SetCurrentHireRole(player, 1);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();
        var consumptionBefore = match.Random.ConsumptionCount;

        recorder.PrepareAiPlanning(player);
        var consumptionAfterPreparation = match.Random.ConsumptionCount;
        var first = Assert.Single(AiTurnPlanner.Plan(match, player));
        var second = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(consumptionBefore + 3, consumptionAfterPreparation);
        Assert.Equal(consumptionAfterPreparation, match.Random.ConsumptionCount);
        Assert.Equal(first, second);
        Assert.Equal(GangAction.Move, first.Action);
        Assert.Contains(first.Target.Id, new[] { 1, 8, 9 });

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void EliminateMovementPrefersRecoveredHeadquartersCandidateSet()
    {
        var match = CreateMatch();
        const int ordinarySector = 10;

        foreach (var headquarters in OriginalCityGenerator.HeadquartersCandidates)
            Assert.True(
                AiTurnPlanner.DestinationValue(
                    match, new PlayerId(0), headquarters, ScenarioId.Eliminate)
                > AiTurnPlanner.DestinationValue(
                    match, new PlayerId(0), ordinarySector, ScenarioId.Eliminate));
    }

    [Fact]
    public void HirePlannerSelectsOnlyAnAffordableValidOfferWithoutMutation()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
        var before = MatchStateHasher.ComputeSha256(match);

        var choice = AiTurnPlanner.ChooseHire(match, new PlayerId(0));

        Assert.Equal(before, MatchStateHasher.ComputeSha256(match));
        if (choice is not null)
            Assert.True(HireRules.Validate(match, new PlayerId(0),
                choice.GangDefinitionId, choice.SectorId).IsValid);
    }

    [Fact]
    public void HiringPreparationUsesZeroBasedOriginalTurnAndAdvancesCurrentRole()
    {
        var match = CreateMatch(scenario: ScenarioId.BigMan);
        var player = new PlayerId(0);
        match.AiPlanning.SetCurrentHireRole(player, 6);
        match.FinishUpkeep();
        match.PrepareAiPlanning(player);

        match.PrepareAiHiring(player);

        Assert.Equal(6, match.AiPlanning.PreviousHireRole(player));
        Assert.Equal(0, match.AiPlanning.CurrentHireRole(player));
    }

    [Fact]
    public void HiringPreparationLeavesRoleUnchangedWhenOriginalAttemptGateFails()
    {
        var match = CreateMatch(
            scenario: ScenarioId.Greed,
            ownsStartingSector: false);
        var player = new PlayerId(0);
        match.AiPlanning.SetCurrentHireRole(player, 6);
        match.FinishUpkeep();
        match.PrepareAiPlanning(player);

        match.PrepareAiHiring(player);

        Assert.Equal(6, match.AiPlanning.CurrentHireRole(player));
    }

    [Fact]
    public void HirePlannerUsesOriginalRoleRankingInsteadOfRecreationScalar()
    {
        var data = BundledOriginalData.Load();
        var affordable = data.Gangs.Where(gang => gang.Id != 0 && gang.Force <= 100).ToArray();
        var offers = affordable
            .SelectMany(first => affordable.Where(second => second.Id != first.Id)
                .Select(second => (first, second)))
            .SelectMany(pair => affordable.Where(third =>
                    third.Id != pair.first.Id && third.Id != pair.second.Id)
                .Select(third => new[] { pair.first, pair.second, third }))
            .First(candidate =>
            {
                var original = OriginalAiHireRules.SelectOfferIndex(
                    candidate, ScenarioId.Power, requestedMode: 0, availableCash: 100);
                var recreationScalar = candidate
                    .Select((gang, index) => (index,
                        score: gang.Force * 20 + gang.TechLevel * 10
                            - gang.Upkeep * 15 - HireRules.InitialCost(gang)))
                    .OrderByDescending(entry => entry.score)
                    .ThenBy(entry => candidate[entry.index].Id)
                    .First().index;
                return original.HasValue && original.Value != recreationScalar;
            });
        var match = CreateMatch(data: data, cash: 100,
            hirePool: offers.Select(gang => gang.Id).ToArray());
        match.FinishUpkeep();
        match.PrepareAiPlanning(new PlayerId(0));
        var expectedIndex = OriginalAiHireRules.SelectOfferIndex(
            offers, ScenarioId.Power, requestedMode: 0,
            availableCash: match.Players[0].Cash)!.Value;

        var choice = match.PrepareAiHiring(new PlayerId(0)).Choice;

        Assert.NotNull(choice);
        Assert.Equal(offers[expectedIndex].Id, choice.GangDefinitionId);
    }

    [Fact]
    public void PreparedHireUsesOriginalFailedRankingRejection()
    {
        var data = BundledOriginalData.Load();
        var offers = data.Gangs
            .Where(gang => gang.Id != 0 && gang.Force > 0)
            .Take(MatchLimits.HireOffersPerPlayer)
            .ToArray();
        var match = CreateMatch(data: data, cash: -100,
            hirePool: offers.Select(gang => gang.Id).ToArray());
        match.FinishUpkeep();
        var selection = new OriginalAiHireRoleSelection(RankingMode: 3, Role: 4);
        var rejectedIndex = OriginalAiHireRules.SelectRejectedOfferIndex(
            offers, ScenarioId.Power);

        var preparation = AiTurnPlanner.PrepareHire(
            match, new PlayerId(0), selection);

        Assert.Null(preparation.Choice);
        Assert.Equal(offers[rejectedIndex].Id, preparation.RejectedGangDefinitionId);
        Assert.True(match.SnubHireOffer(
            new PlayerId(0), preparation.RejectedGangDefinitionId!.Value).Accepted);
    }

    [Fact]
    public void PreparedHireRefreshesAFullAnchorAndUsesRecoveredPlacementSector()
    {
        short[] offers = [1, 2, 3];
        var match = CreateMatch(cash: 100, hirePool: offers);
        var playerId = new PlayerId(0);
        match.Sectors[10].Owner = playerId;
        for (var index = 0; index < MatchLimits.FriendlyGangsPerSector - 1; index++)
            match.Players[0].AddGang(new MatchGangState(
                new GangId(30 + index), playerId, 1, sectorId: 0, force: 5));
        match.FinishUpkeep();

        var preparation = AiTurnPlanner.PrepareHire(
            match, playerId, new OriginalAiHireRoleSelection(RankingMode: 0, Role: 0));

        Assert.NotNull(preparation.Choice);
        Assert.Equal(10, preparation.Choice.SectorId);
        Assert.Equal(10 + AiPlanningState.SectorAnchorOffset,
            match.AiPlanning.SectorAnchor(playerId));
    }

    [Fact]
    public void RoleFourPlacementUsesFirstVisibleHostileSectorRegardlessOfController()
    {
        var data = BundledOriginalData.Load();
        var observerDefinition = data.Gangs.MaxBy(gang => gang.Stats.Detect)!.Id;
        var targetDefinition = data.Gangs.MinBy(gang => gang.Stats.Stealth)!.Id;
        short[] offers = [1, 2, 3];
        var match = CreateMatch(
            data: data, definitionId: observerDefinition, rivalDefinitionId: targetDefinition,
            cash: 100, hirePool: offers, rivalController: PlayerController.Computer);
        var playerId = new PlayerId(0);
        match.Sectors[1].Owner = playerId;
        match.Players[0].AddGang(new MatchGangState(
            new GangId(30), playerId, observerDefinition, sectorId: 1, force: 5));
        match.AiStrategy.RecordCombat(new PlayerId(1), playerId, damage: 1);
        match.FinishUpkeep();

        var preparation = AiTurnPlanner.PrepareHire(
            match, playerId, new OriginalAiHireRoleSelection(RankingMode: 0, Role: 4));

        Assert.NotNull(preparation.Choice);
        Assert.Equal(1, preparation.Choice.SectorId);
    }

    [Theory]
    [InlineData(ScenarioId.Greed)]
    [InlineData(ScenarioId.Power)]
    [InlineData(ScenarioId.Acceptance)]
    [InlineData(ScenarioId.Dominance)]
    public void AllComputerTimedMatchCompletesDeterministicallyAndReplays(ScenarioId scenario)
    {
        var first = DriveMatch(scenario, 1984);
        var second = DriveMatch(scenario, 1984);

        Assert.NotNull(first.State.Outcome);
        Assert.Equal(MatchEndReason.TimeLimit, first.State.Outcome!.Reason);
        Assert.Equal(MatchStateHasher.ComputeSha256(first.State), MatchStateHasher.ComputeSha256(second.State));
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, first);
        replay.Position = 0;
        var replayed = MatchReplaySerializer.LoadAndReplay(replay, first.State.Definitions);
        Assert.Equal(MatchStateHasher.ComputeSha256(first.State), MatchStateHasher.ComputeSha256(replayed));
    }

    [Theory]
    [InlineData(ScenarioId.KillEmAll)]
    [InlineData(ScenarioId.Big40)]
    [InlineData(ScenarioId.Eliminate)]
    [InlineData(ScenarioId.Siege)]
    [InlineData(ScenarioId.BigMan)]
    [InlineData(ScenarioId.Armageddon)]
    public void AllComputerObjectiveMatchRunsDeterministicReplayWindow(ScenarioId scenario)
    {
        var first = DriveMatch(scenario, 2112, throughTurn: 20);
        var second = DriveMatch(scenario, 2112, throughTurn: 20);

        Assert.True(first.State.Outcome is not null || first.State.Coordinator.Turn > 20);
        Assert.Equal(MatchStateHasher.ComputeSha256(first.State), MatchStateHasher.ComputeSha256(second.State));
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, first);
        replay.Position = 0;
        var replayed = MatchReplaySerializer.LoadAndReplay(replay, first.State.Definitions);
        Assert.Equal(MatchStateHasher.ComputeSha256(first.State), MatchStateHasher.ComputeSha256(replayed));
    }

    private static MatchReplayRecorder DriveMatch(ScenarioId scenario, int seed, int? throughTurn = null)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU ONE", PlayerController.Computer),
            new(new PlayerId(1), "CPU TWO", PlayerController.Computer)
        ];
        var recorder = new MatchReplayRecorder(OriginalMatchFactory.Create(
            data, new MatchSetup(scenario, GameDuration.SixMonths, seed, setups)));
        var boundaries = 0;
        while (recorder.State.Outcome is null
               && (throughTurn is null || recorder.State.Coordinator.Turn <= throughTurn)
               && boundaries++ < 1_000)
        {
            var state = recorder.State;
            switch (state.Coordinator.Phase)
            {
                case TurnPhase.Upkeep:
                    recorder.FinishUpkeep();
                    break;
                case TurnPhase.Command:
                    var commandPlayer = state.Coordinator.ActivePlayer!.Value;
                    recorder.PrepareAiPlanning(commandPlayer);
                    foreach (var command in AiTurnPlanner.Plan(state, commandPlayer))
                        Assert.True(recorder.Submit(command).Accepted);
                    var hiring = recorder.PrepareAiHiring(commandPlayer);
                    if (hiring.Choice is { } planningHire)
                        Assert.True(recorder.QueueHire(commandPlayer,
                            planningHire.GangDefinitionId, planningHire.SectorId).Accepted);
                    else if (hiring.RejectedGangDefinitionId is { } rejectedOffer)
                        Assert.True(recorder.SnubHireOffer(commandPlayer, rejectedOffer).Accepted);
                    recorder.FinishCommand(commandPlayer);
                    break;
                case TurnPhase.Execution:
                    recorder.FinishExecutionPhase();
                    break;
                case TurnPhase.Hire:
                    var hiringPlayer = state.Coordinator.ActivePlayer!.Value;
                    recorder.FinishHire(hiringPlayer);
                    break;
                case TurnPhase.PlayerElimination:
                    recorder.FinishPlayerElimination();
                    break;
            }
        }
        Assert.True(boundaries < 1_000, "AI match exceeded the phase-boundary safety limit.");
        return recorder;
    }

    private static MatchState CreateMatch(
        PlayerController controller = PlayerController.Computer,
        AiDifficulty difficulty = AiDifficulty.Criminal,
        short definitionId = 1,
        int force = 10,
        ScenarioId scenario = ScenarioId.Power,
        bool ownsStartingSector = true,
        OriginalData? data = null,
        int cash = 20,
        IReadOnlyList<short>? hirePool = null,
        PlayerController rivalController = PlayerController.Human,
        short rivalDefinitionId = 2,
        IReadOnlySet<short>? researchedItems = null,
        int startingSector = 0)
    {
        data ??= BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", controller),
            new(new PlayerId(1), "RIVAL", rivalController)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], cash,
                [new MatchGangState(new GangId(10), new PlayerId(0), definitionId, startingSector, force)],
                hirePool,
                researchedItems: researchedItems),
            new(setups[1], 20, [new MatchGangState(
                new GangId(20), new PlayerId(1), rivalDefinitionId, 1, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ], owner: ownsStartingSector && id == startingSector ? new PlayerId(0) : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, 7, setups, difficulty), players, sectors);
    }

    private static MatchState CreateNeutralControlMatch(
        OriginalData data,
        short definitionId,
        int force)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), new PlayerId(0), definitionId, 0, force)]),
            new(setups[1], 20, [])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 5),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 5)
            ], income: ManualRules.MinimumSectorIncome))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, 17, setups), players, sectors);
    }
}
