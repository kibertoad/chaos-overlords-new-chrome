using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyZeroTurnPlannerTests
{
    [Fact]
    public void FirstTurnLowForceGangHeals()
    {
        var match = CreateMatch(force: 7);
        var player = new PlayerId(0);
        BeginFamilyZeroTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));
    }

    [Fact]
    public void FirstHealthyGangHidesWhenSectorHasNoPreviousHide()
    {
        var match = CreateMatch();
        var player = new PlayerId(0);
        BeginFamilyZeroTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.Hide,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void PreviousAttackWithoutWeightTenOpponentMovesByModeFive()
    {
        var match = CreateMatch(targetSector: 63);
        var player = new PlayerId(0);
        BeginFamilyZeroTurn(match, player);
        SetPreviousAction(match, player, GangAction.Attack);
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));
    }

    [Fact]
    public void PreviousAttackMakesSingleAcceptedWeightTenDraw()
    {
        var match = CreateMatch(targetSector: 0, targetForce: 1);
        var player = new PlayerId(0);
        BeginFamilyZeroTurn(match, player);
        SetPreviousAction(match, player, GangAction.Attack);
        match.FinishUpkeep();
        var randomBefore = match.Random.ConsumptionCount;

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
        Assert.Equal(3, match.Random.ConsumptionCount - randomBefore);
        Assert.Equal(0, match.AiPlanning.FocusValue(player, 0));
    }

    [Fact]
    public void FailedPostHealCombatDrawPreservesNoActionAndClearsBothAuxiliaries()
    {
        var match = CreateMatch(
            targetSector: 0, force: 8, targetForce: 10, weakAttacker: true);
        var player = new PlayerId(0);
        BeginFamilyZeroTurn(match, player);
        SetPreviousAction(match, player, GangAction.Heal);
        match.AiPlanning.SetFocusValue(player, 0, 22);
        match.AiPlanning.SetCoverageSector(player, 0, 23);
        var attacker = match.Players[0].Gangs[0];
        var target = match.Players[1].Gangs[0];
        var attackerStats = EffectiveStatisticsCalculator.ForGang(match, attacker);
        var targetStats = EffectiveStatisticsCalculator.ForGang(match, target);
        Assert.False(OriginalAiFamilyZeroRules.CanAttackSelectedTarget(
            attacker.Force, attackerStats.Combat, attackerStats.Defense,
            target.Force, targetStats.Combat, targetStats.Defense));
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Empty(AiTurnPlanner.Plan(match, player));
        Assert.Equal(GangAction.None, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));
        Assert.Equal(AiPlanningState.InactiveCoverageSector,
            match.AiPlanning.CoverageSector(player, 0));
    }

    [Fact]
    public void PreviousHideRetriesFiveFailedDrawsThenAttacksFinalTarget()
    {
        var match = CreateMatch(
            targetSector: 0, force: 8, targetForce: 10, weakAttacker: true);
        var player = new PlayerId(0);
        BeginFamilyZeroTurn(match, player);
        SetPreviousAction(match, player, GangAction.Hide);
        match.FinishUpkeep();
        var randomBefore = match.Random.ConsumptionCount;

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
        Assert.Equal(15, match.Random.ConsumptionCount - randomBefore);
    }

    [Fact]
    public void PreviousHideUsesNearbyDangerWeaponBeforeArmorOpportunity()
    {
        var match = CreateMatch(equipmentOpportunity: true);
        var player = new PlayerId(0);
        BeginFamilyZeroTurn(match, player);
        SetPreviousAction(match, player, GangAction.Hide);
        var expected = Assert.IsType<OriginalAiEquipmentRules.Upgrade>(
            OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                match, match.Players[0], match.Players[0].Gangs[0], 0));
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(expected.ItemId), command.Target);
        Assert.Equal(match.Definitions.Items[expected.ItemId].Cost * 3,
            expected.Slot == EquipmentSlot.Weapon
                ? match.AiPlanning.WeaponCooldown(player, 0)
                : match.AiPlanning.ArmorCooldown(player, 0));
    }

    [Fact]
    public void FailedPostHealNoActionRoundTripsThroughReplay()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(
            data, targetSector: 0, force: 8, targetForce: 10, weakAttacker: true);
        var player = new PlayerId(0);
        BeginFamilyZeroTurn(match, player);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Heal);
        match.AiPlanning.SetFocusValue(player, 0, 22);
        match.AiPlanning.SetCoverageSector(player, 0, 23);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);

        Assert.Empty(AiTurnPlanner.Plan(match, player));
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Theory]
    [InlineData(ScenarioId.Power, GangAction.Move, GangAction.Move, 2)]
    [InlineData(ScenarioId.Siege, GangAction.Move, GangAction.Move, 11)]
    [InlineData(ScenarioId.Power, GangAction.Move, GangAction.Attack, null)]
    [InlineData(ScenarioId.Power, GangAction.Hide, GangAction.Move, null)]
    public void FamilyTransitionUsesPlannedAndOlderActions(
        ScenarioId scenario,
        GangAction plannedAction,
        GangAction olderAction,
        int? expected) =>
        Assert.Equal(expected, OriginalAiFamilyZeroRules.FamilyAfterPlanning(
            scenario, plannedAction, olderAction));

    private static void BeginFamilyZeroTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 0);
        match.AiPlanning.SetCurrentHireRole(player, 0);
    }

    private static void SetPreviousAction(
        MatchState match,
        PlayerId player,
        GangAction action)
    {
        match.AiPlanning.SetPlannedAction(player, 0, action);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
    }

    private static MatchState CreateMatch(
        int targetSector = 63,
        int force = 10,
        int targetForce = 10,
        bool weakAttacker = false,
        bool equipmentOpportunity = false) =>
        CreateMatch(BundledOriginalData.Load(), targetSector, force, targetForce,
            weakAttacker, equipmentOpportunity);

    private static MatchState CreateMatch(
        OriginalData data,
        int targetSector,
        int force,
        int targetForce,
        bool weakAttacker,
        bool equipmentOpportunity = false)
    {
        var pair = (from attackerCandidate in data.Gangs
                    from targetCandidate in data.Gangs
                    where attackerCandidate.Stats.Detect >= targetCandidate.Stats.Stealth
                    let accepted = OriginalAiFamilyZeroRules.CanAttackSelectedTarget(
                        force,
                        attackerCandidate.Stats.Combat,
                        attackerCandidate.Stats.Defense,
                        targetForce,
                        targetCandidate.Stats.Combat,
                        targetCandidate.Stats.Defense)
                    where accepted != weakAttacker
                    orderby weakAttacker
                        ? attackerCandidate.Stats.Combat + attackerCandidate.Stats.Defense
                        : -(attackerCandidate.Stats.Combat + attackerCandidate.Stats.Defense)
                    select (Attacker: attackerCandidate, Target: targetCandidate)).First();
        var attacker = pair.Attacker;
        var target = pair.Target;
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "HUMAN", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], equipmentOpportunity ? 500 : 20,
                [new MatchGangState(new GangId(10), setups[0].Id, attacker.Id, 0, force)],
                researchedItems: equipmentOpportunity
                    ? data.Items.Select((item, index) => (item, index))
                        .Where(entry => entry.item.Type != 99)
                        .Select(entry => checked((short)entry.index))
                        .ToHashSet()
                    : []),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, target.Id,
                    targetSector, targetForce)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 5, 15)
            ], owner: id == 0 && targetSector == 0 || equipmentOpportunity && id == 1
                    ? setups[1].Id
                    : setups[0].Id))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, 41, setups,
            AiDifficulty.HomicidalManiac), players, sectors);
    }
}
