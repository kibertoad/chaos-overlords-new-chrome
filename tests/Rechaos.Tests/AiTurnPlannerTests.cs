using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class AiTurnPlannerTests
{
    [Fact]
    public void PlannerIsDeterministicNonMutatingAndReturnsOnlyLegalCommands()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        match.PrepareAiPlanning(new PlayerId(0));
        var before = MatchStateHasher.ComputeFingerprint(match);

        var first = AiTurnPlanner.Plan(match, new PlayerId(0));
        var second = AiTurnPlanner.Plan(match, new PlayerId(0));

        Assert.Equal(before, MatchStateHasher.ComputeFingerprint(match));
        Assert.Equal(first, second);
        Assert.Single(first);
        Assert.All(first, command => Assert.True(CommandValidator.Validate(match, command).IsValid));
        Assert.Equal(first.Count, first.Select(command => command.Gang).Distinct().Count());
    }

    [Fact]
    public void SharedEquipmentBudgetFollowsRosterSlotsAfterGangIdOrderChanges()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Where(item => item.Type != 99)
            .Select(item => checked((short)item.Id))
            .ToHashSet();
        var match = CreateMatch(data: data, cash: 1_000, researchedItems: researched);
        var playerId = new PlayerId(0);
        var player = match.Players[0];
        player.ReplaceGang(0, new MatchGangState(new GangId(30), playerId, 1, 0, 10));
        player.AddGang(new MatchGangState(new GangId(11), playerId, 1, 0, 10));
        match.FinishUpkeep();

        var secondTargets = CommandOptionCatalog.LegalCommands(match, playerId, new GangId(11))
            .Where(command => command.Action == GangAction.Equip)
            .Select(command => command.Target.Id)
            .ToHashSet();
        var itemId = CommandOptionCatalog.LegalCommands(match, playerId, new GangId(30))
            .Where(command => command.Action == GangAction.Equip)
            .Select(command => command.Target.Id)
            .First(id => secondTargets.Contains(id) && data.Items[id].Cost > 0);
        player.Cash = data.Items[itemId].Cost;
        match.AiPlanning.BeginPlanning(playerId);
        for (var slot = 0; slot < 2; slot++)
        {
            match.AiPlanning.SeedFamily(playerId, slot, 1);
            match.AiPlanning.SetPlannedAction(playerId, slot, GangAction.Equip,
                new AiActionTarget(checked((byte)itemId), 0));
        }
        match.MarkAiPlanningPrepared(playerId);

        var command = Assert.Single(AiTurnPlanner.Plan(match, playerId));

        Assert.Equal(new GangId(30), command.Gang);
        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(itemId), command.Target);
    }

    [Fact]
    public void GangLeftInFamilyNinetyNinePlansNothing()
    {
        var match = CreateMatch();
        var playerId = new PlayerId(0);
        match.Players[0].AddGang(new MatchGangState(new GangId(11), playerId, 1, 0, 10));
        match.FinishUpkeep();

        match.PrepareAiPlanning(playerId);
        var commands = AiTurnPlanner.Plan(match, playerId);

        // RULE-AI-001, RULE-AI-002: the first pass flags only slot 0, so the gang in slot 1 stays
        // in family 99, which has no handler, and gets no order.
        Assert.NotEqual(AiPlanningState.UnusedFamily, match.AiPlanning.Family(playerId, 0));
        Assert.Equal(AiPlanningState.UnusedFamily, match.AiPlanning.Family(playerId, 1));
        Assert.DoesNotContain(commands, command => command.Gang == new GangId(11));
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
    public void PlannerRefusesRecordsTheDispatchDidNotWriteThisTurn()
    {
        var match = CreateMatch();
        var player = new PlayerId(0);
        match.FinishUpkeep();

        // RULE-AI-002: before the first pass there are no records to plan from.
        Assert.Throws<InvalidOperationException>(() => AiTurnPlanner.Plan(match, player));

        match.PrepareAiPlanning(player);
        AiTurnPlanner.Plan(match, player);

        // A later turn: the player has planned before, but this turn's records are last turn's
        // orders until the pass rolls them forward.
        var coordinator = match.Coordinator;
        for (var id = 0; id < match.Players.Count; id++)
            coordinator.FinishCommand(new PlayerId(id));
        foreach (var _ in TurnStructure.ExecutionOrder)
            coordinator.FinishExecutionPhase();
        for (var id = 0; id < match.Players.Count; id++)
            coordinator.FinishHire(new PlayerId(id));
        coordinator.FinishPlayerElimination();
        coordinator.FinishUpkeep();
        Assert.Equal(2, coordinator.Turn);
        Assert.Equal(player, coordinator.ActivePlayer);
        Assert.True(match.AiPlanning.HasPlanned(player));
        Assert.Throws<InvalidOperationException>(() => AiTurnPlanner.Plan(match, player));

        match.PrepareAiPlanning(player);
        AiTurnPlanner.Plan(match, player);
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
        match.PrepareAiPlanning(new PlayerId(0));

        Assert.False(match.CanPlayerDetectGang(new PlayerId(0), new GangId(20)));
        Assert.DoesNotContain(CommandOptionCatalog.LegalCommands(match, new PlayerId(0), new GangId(10)),
            command => command.Action == GangAction.Attack);
        Assert.DoesNotContain(AiTurnPlanner.Plan(match, new PlayerId(0)),
            command => command.Action == GangAction.Attack);
    }

    [Fact]
    public void PlanFromThePassIsLegalAndConsumesNoRandomnessUnderEitherMentality()
    {
        var goon = CreateMatch(difficulty: AiDifficulty.Goon);
        var crimeLord = CreateMatch(difficulty: AiDifficulty.CrimeLord);
        goon.FinishUpkeep();
        crimeLord.FinishUpkeep();
        goon.PrepareAiPlanning(new PlayerId(0));
        crimeLord.PrepareAiPlanning(new PlayerId(0));
        var goonConsumption = goon.Random.ConsumptionCount;
        var crimeLordConsumption = crimeLord.Random.ConsumptionCount;

        var goonPlan = AiTurnPlanner.Plan(goon, new PlayerId(0));
        var crimeLordPlan = AiTurnPlanner.Plan(crimeLord, new PlayerId(0));

        Assert.All(goonPlan.Concat(crimeLordPlan), command =>
            Assert.True(CommandValidator.Validate(
                command.Player == new PlayerId(0) && goonPlan.Contains(command) ? goon : crimeLord,
                command).IsValid));
        Assert.Equal(goonConsumption, goon.Random.ConsumptionCount);
        Assert.Equal(crimeLordConsumption, crimeLord.Random.ConsumptionCount);
    }

    [Fact]
    public void SoloControlRequiresStrictAdvantageAtOriginalBoundary()
    {
        var data = BundledOriginalData.Load();
        var definition = data.Gangs.First(candidate =>
            ManualRules.MinimumSectorIncome - candidate.Stats.Control
                is >= 1 and < ManualRules.MaximumForce);
        var equalForce = ManualRules.MinimumSectorIncome - definition.Stats.Control;
        var equal = CreateNeutralControlMatch(data, definition.Id, equalForce);
        var advantage = CreateNeutralControlMatch(data, definition.Id, equalForce + 1);
        equal.FinishUpkeep();
        advantage.FinishUpkeep();

        var equalGang = equal.FindGang(new GangId(10))!;
        var advantageGang = advantage.FindGang(new GangId(10))!;
        Assert.False(AiTurnPlanner.CanSoloControl(equal, new PlayerId(0), equalGang));
        Assert.True(AiTurnPlanner.CanSoloControl(advantage, new PlayerId(0), advantageGang));
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
            match.AiPlanning.BeginPlanning(new PlayerId(0));
            match.AiPlanning.SeedFamily(new PlayerId(0), 0, 1);
        }
        crackdown.Sectors[0].CrackdownActive = true;
        priorSnitch.AiPlanning.SetPlannedAction(new PlayerId(0), 0, GangAction.Snitch);
        priorSnitch.AiPlanning.RollActiveGangActions(
            new PlayerId(0), priorSnitch.Players[0].Gangs);
        // The pass rolls the records again, so the Snitch becomes the older action.
        foreach (var match in new[] { heal, crackdown, priorSnitch })
            match.PrepareAiPlanning(new PlayerId(0));

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
            match.AiPlanning.BeginPlanning(new PlayerId(0));
            match.AiPlanning.SeedFamily(new PlayerId(0), 0, 1);
            match.AiPlanning.SetPlannedAction(new PlayerId(0), 0, GangAction.Heal);
            // The pass rolls the Heal into the previous action and runs family 1's handler.
            match.PrepareAiPlanning(new PlayerId(0));
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
        match.AiPlanning.SeedFamily(player, 0, 1);
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
        match.AiPlanning.SeedFamily(player, 0, 1);
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
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
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
        match.AiPlanning.SeedFamily(player, 0, 1);
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
    [InlineData(ScenarioId.Siege, 2, 14, 20, 12)]
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
        match.AiPlanning.BeginPlanning(player);
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
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
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
        match.AiPlanning.SeedFamily(player, 0, 14);
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
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(13, restored.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Heal, restored.AiPlanning.PlannedAction(player, 0));
    }

    [Theory]
    [InlineData(ScenarioId.BigMan, 27)]
    [InlineData(ScenarioId.Siege, 9)]
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
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
        Assert.Equal(GangAction.Heal, restored.AiPlanning.PlannedAction(player, 0));
    }

    [Fact]
    public void ContestedObjectiveSelectsExactVisibleOwnerGangAndReplaysAttack()
    {
        var data = BundledOriginalData.Load();
        var attackerDefinition = data.Gangs
            .OrderByDescending(candidate => candidate.Stats.Detect)
            .ThenByDescending(candidate => candidate.Stats.Combat)
            .First();
        var defenderDefinition = data.Gangs
            .OrderBy(candidate => candidate.Stats.Stealth)
            .ThenBy(candidate => candidate.Stats.Defense)
            .First();
        var match = CreateMatch(
            definitionId: attackerDefinition.Id,
            force: 10,
            scenario: ScenarioId.BigMan,
            ownsStartingSector: false,
            startingSector: 27,
            data: data,
            rivalDefinitionId: defenderDefinition.Id);
        var player = new PlayerId(0);
        match.Players[1].Gangs[0].SectorId = 27;
        match.Sectors[27].Owner = new PlayerId(1);
        // RULE-AI-031: the gang fights when turns_remaining() is even, and Big Man plays with a
        // turn limit of 65535 (FND-SETUP-018), so the second turn is a fighting turn.
        var coordinator = match.Coordinator;
        coordinator.FinishUpkeep();
        foreach (var setup in match.Setup.Players) coordinator.FinishCommand(setup.Id);
        foreach (var _ in TurnStructure.ExecutionOrder) coordinator.FinishExecutionPhase();
        foreach (var setup in match.Setup.Players) coordinator.FinishHire(setup.Id);
        coordinator.FinishPlayerElimination();
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 1);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();
        var consumptionBefore = match.Random.ConsumptionCount;

        recorder.PrepareAiPlanning(player);
        var expectedAttack = new GameCommand(
            player,
            new GangId(10),
            GangAction.Attack,
            CommandTarget.Gang(new GangId(20)));
        var validation = CommandValidator.Validate(match, expectedAttack);
        Assert.True(validation.IsValid, validation.Code.ToString());
        Assert.Contains(expectedAttack,
            CommandOptionCatalog.LegalCommands(match, player, new GangId(10)));
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.True(match.CanPlayerDetectGang(player, new GangId(20)));
        Assert.False(match.AiStrategy.IsHostile(player, new PlayerId(1)));
        Assert.Equal(13, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Attack, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(1, 0), match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
        Assert.Equal(consumptionBefore + 3, match.Random.ConsumptionCount);

        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void FamilyOnePreparationUsesRecoveredModeFiveMoveDestination()
    {
        var data = BundledOriginalData.Load();
        var controller = data.Gangs.MaxBy(candidate => candidate.Stats.Control)!.Id;
        var match = CreateMatch(definitionId: controller, force: 8, data: data);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
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
    public void PreparedRecoveredMoveToSourcePlansNothing()
    {
        var match = CreateMatch();
        var player = new PlayerId(0);
        match.FinishUpkeep();
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SeedFamily(player, 0, 1);
        match.AiPlanning.SetPlannedAction(
            player, 0, GangAction.Move, new AiActionTarget(0, 0));
        match.MarkAiPlanningPrepared(player);

        Assert.Empty(AiTurnPlanner.Plan(match, player));
        Assert.Equal(GangAction.Move, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(0, 0), match.AiPlanning.PlannedTarget(player, 0));
    }

    [Fact]
    public void ModeFiveTieConsumesOneBoundedDrawDuringReplayablePreparationOnly()
    {
        var data = BundledOriginalData.Load();
        var controller = data.Gangs.MaxBy(candidate => candidate.Stats.Control)!.Id;
        var match = CreateMatch(definitionId: controller, force: 8, data: data);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
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
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match),
            MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void EliminateMovementGivesHeadquartersCandidatesNoObjectiveBonus()
    {
        // The six headquarters sectors are scenario 6's (Siege) objective list
        // (RULE-AI-031); Eliminate (scenario 7) has no objective sectors.
        var match = CreateMatch(scenario: ScenarioId.Eliminate);
        const int ordinarySector = 10;

        foreach (var headquarters in OriginalCityGenerator.HeadquartersCandidates)
            Assert.Equal(
                AiTurnPlanner.DestinationValue(
                    match, new PlayerId(0), ordinarySector, ScenarioId.Eliminate),
                AiTurnPlanner.DestinationValue(
                    match, new PlayerId(0), headquarters, ScenarioId.Eliminate));
    }

    // RULE-AI-020, FND-AI-057: after previous Move, a family-1 gang in its own sector moves
    // through mode 5, and one elsewhere heals when it can.
    [Theory]
    [InlineData(true, 10, GangAction.Move)]
    [InlineData(false, 8, GangAction.Heal)]
    public void FamilyOneAfterMoveMovesOnOrHeals(bool ownsSector, int force, GangAction expected)
    {
        var match = CreateMatch(force: force, ownsStartingSector: ownsSector);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SeedFamily(player, 0, 1);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Move);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
        match.FinishUpkeep();

        AiHandlerPass.Run(match, player);

        Assert.Equal(expected, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(AiPlanningState.InactiveFocusValue, match.AiPlanning.FocusValue(player, 0));
    }

    // RULE-AI-020: every action family 1 writes but the Attack clears the focus, whatever the
    // previous action was.
    [Theory]
    [InlineData(GangAction.None, 5, GangAction.Heal)]
    [InlineData(GangAction.Chaos, 5, GangAction.Heal)]
    [InlineData(GangAction.Heal, 8, GangAction.Heal)]
    [InlineData(GangAction.Heal, 10, GangAction.Move)]
    public void FamilyOneClearsAStaleFocusOnEveryActionButAttack(
        GangAction previous, int force, GangAction expected)
    {
        var data = BundledOriginalData.Load();
        var capable = data.Gangs.First(candidate => candidate.Stats.Heal >= -3).Id;
        var match = CreateMatch(definitionId: capable, force: force, data: data);
        var player = new PlayerId(0);
        match.FinishUpkeep();
        match.AiPlanning.SeedFamily(player, 0, 1);
        match.AiPlanning.SetPlannedAction(player, 0, previous);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
        match.AiPlanning.SetFocusValue(player, 0, 7);

        AiHandlerPass.Run(match, player);

        Assert.Equal(expected, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(AiPlanningState.InactiveFocusValue, match.AiPlanning.FocusValue(player, 0));
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
