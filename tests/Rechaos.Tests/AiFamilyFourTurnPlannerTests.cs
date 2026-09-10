using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyFourTurnPlannerTests
{
    [Fact]
    public void PreviousNoneUsesHealThenHideBoundary()
    {
        var lowForce = CreateMatch(force: 7);
        var healthy = CreateMatch(force: 8);
        BeginFamilyFourTurn(lowForce);
        BeginFamilyFourTurn(healthy);
        lowForce.FinishUpkeep();
        healthy.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(lowForce, new PlayerId(0));
        AiTurnPlanner.PrepareRecoveredFamilyCommands(healthy, new PlayerId(0));

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(lowForce, new PlayerId(0))).Action);
        Assert.Equal(GangAction.Hide,
            Assert.Single(AiTurnPlanner.Plan(healthy, new PlayerId(0))).Action);
    }

    [Fact]
    public void PreviousMoveWithoutWeightTenUsesModeTwo()
    {
        var match = CreateMatch(targetSector: 63);
        match.Sectors[0].Owner = new PlayerId(1);
        BeginFamilyFourTurn(match);
        SetPreviousAction(match, GangAction.Move);
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, new PlayerId(0));
        var command = Assert.Single(AiTurnPlanner.Plan(match, new PlayerId(0)));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(new PlayerId(0), 0));
    }

    [Fact]
    public void FailedWeightTenDrawAfterMoveClearsActionAndBothAuxiliaries()
    {
        var match = CreateMatch(
            targetSector: 0, force: 8, targetForce: 10, weakAttacker: true);
        var player = new PlayerId(0);
        BeginFamilyFourTurn(match);
        SetPreviousAction(match, GangAction.Move);
        match.AiPlanning.SetFocusValue(player, 0, 22);
        match.AiPlanning.SetCoverageSector(player, 0, 23);
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
    public void PreviousHideRetriesFiveFailedDrawsThenAttacks()
    {
        var match = CreateMatch(
            targetSector: 0, force: 8, targetForce: 10, weakAttacker: true);
        var player = new PlayerId(0);
        BeginFamilyFourTurn(match);
        SetPreviousAction(match, GangAction.Hide);
        match.FinishUpkeep();
        var randomBefore = match.Random.ConsumptionCount;

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
        Assert.Equal(15, match.Random.ConsumptionCount - randomBefore);
    }

    [Fact]
    public void PreviousHideUsesNearbyDangerEquipmentOpportunity()
    {
        var match = CreateMatch(equipmentOpportunity: true);
        var player = new PlayerId(0);
        BeginFamilyFourTurn(match);
        SetPreviousAction(match, GangAction.Hide);
        var expected = Assert.IsType<OriginalAiEquipmentRules.Upgrade>(
            OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                match, match.Players[0], match.Players[0].Gangs[0], 0));
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(expected.ItemId), command.Target);
    }

    [Fact]
    public void UnmappedGreedRolePreservesFamilyFourAndReplaysLivePlanning()
    {
        var match = CreateMatch(scenario: ScenarioId.Greed);
        var player = new PlayerId(0);
        BeginFamilyFourTurn(match);
        match.AiPlanning.SetCurrentHireRole(player, 5);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);

        Assert.Equal(4, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Hide,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, match.Definitions);
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Theory]
    [InlineData(GangAction.Move, GangAction.Move, true, true)]
    [InlineData(GangAction.Move, GangAction.Attack, true, false)]
    [InlineData(GangAction.Attack, GangAction.Move, true, false)]
    [InlineData(GangAction.Move, GangAction.Move, false, false)]
    public void RepeatedMoveControlGateUsesBothHistoryGenerations(
        GangAction previous,
        GangAction older,
        bool canSolo,
        bool expected) =>
        Assert.Equal(expected, OriginalAiFamilyFourRules.CanControlAfterRepeatedMove(
            previous, older, canSolo));

    private static void BeginFamilyFourTurn(MatchState match)
    {
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 4);
    }

    private static void SetPreviousAction(MatchState match, GangAction action)
    {
        var player = new PlayerId(0);
        match.AiPlanning.SetPlannedAction(player, 0, action);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
    }

    private static MatchState CreateMatch(
        int targetSector = 63,
        int force = 10,
        int targetForce = 10,
        bool weakAttacker = false,
        bool equipmentOpportunity = false,
        ScenarioId scenario = ScenarioId.Power)
    {
        var data = BundledOriginalData.Load();
        var pair = (from attackerCandidate in data.Gangs
                    from targetCandidate in data.Gangs
                    where attackerCandidate.Stats.Detect >= targetCandidate.Stats.Stealth
                    let accepted = OriginalAiFamilyFourRules.CanAttackSelectedTarget(
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
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "HUMAN", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], equipmentOpportunity ? 500 : 20,
                [new MatchGangState(new GangId(10), setups[0].Id, pair.Attacker.Id, 0, force)],
                researchedItems: equipmentOpportunity
                    ? data.Items.Select((item, index) => (item, index))
                        .Where(entry => entry.item.Type != 99)
                        .Select(entry => checked((short)entry.index))
                        .ToHashSet()
                    : []),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, pair.Target.Id,
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
            scenario, GameDuration.SixMonths, 41, setups,
            AiDifficulty.HomicidalManiac), players, sectors);
    }
}
