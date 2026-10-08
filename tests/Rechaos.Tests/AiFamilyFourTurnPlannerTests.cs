using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyFourTurnPlannerTests
{
    [Fact]
    public void PreviousNoneUsesHealThenChaosBoundary()
    {
        var lowForce = CreateMatch(force: 7);
        var healthy = CreateMatch(force: 8);
        BeginFamilyFourTurn(lowForce);
        BeginFamilyFourTurn(healthy);
        lowForce.FinishUpkeep();
        healthy.FinishUpkeep();

        AiHandlerPass.Run(lowForce, new PlayerId(0));
        AiHandlerPass.Run(healthy, new PlayerId(0));

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(lowForce, new PlayerId(0))).Action);
        Assert.Equal(GangAction.Chaos,
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

        AiHandlerPass.Run(match, new PlayerId(0));
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

        AiHandlerPass.Run(match, player);

        Assert.Empty(AiTurnPlanner.Plan(match, player));
        Assert.Equal(GangAction.None, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(AiPlanningState.InactiveFocusValue,
            match.AiPlanning.FocusValue(player, 0));
        Assert.Equal(AiPlanningState.InactiveCoverageSector,
            match.AiPlanning.CoverageSector(player, 0));
    }

    [Fact]
    public void PreviousChaosRetriesFiveFailedDrawsThenAttacks()
    {
        var match = CreateMatch(
            targetSector: 0, force: 8, targetForce: 10, weakAttacker: true);
        var player = new PlayerId(0);
        BeginFamilyFourTurn(match);
        SetPreviousAction(match, GangAction.Chaos);
        match.FinishUpkeep();
        var randomBefore = match.Random.ConsumptionCount;

        AiHandlerPass.Run(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
        Assert.Equal(15, match.Random.ConsumptionCount - randomBefore);
    }

    [Fact]
    public void PreviousChaosUsesNearbyDangerEquipmentOpportunity()
    {
        var match = CreateMatch(equipmentOpportunity: true);
        var player = new PlayerId(0);
        BeginFamilyFourTurn(match);
        SetPreviousAction(match, GangAction.Chaos);
        var expected = Assert.IsType<OriginalAiEquipmentRules.Upgrade>(
            OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                match, match.Players[0], match.Players[0].Gangs[0], 0));
        match.FinishUpkeep();

        AiHandlerPass.Run(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(expected.ItemId), command.Target);
    }

    // RULE-AI-023, FND-AI-046: the count includes the planning gang. After Chaos in an owned
    // sector the gang raises Chaos again while the count is below 2, so alone it stays, and with
    // one more gang that raised Chaos there it moves on.
    [Theory]
    [InlineData(false, GangAction.Chaos)]
    [InlineData(true, GangAction.Move)]
    public void PreviousChaosCountIncludesThePlanningGang(
        bool secondChaosGang,
        GangAction expected)
    {
        var match = CreateMatch();
        var player = new PlayerId(0);
        BeginFamilyFourTurn(match);
        match.Players[0].AddGang(new MatchGangState(new GangId(11), player, 1, 0, 10));
        match.AiPlanning.SeedFamily(player, 1, 4);
        match.AiPlanning.SetPlannedAction(player, 0, GangAction.Chaos);
        match.AiPlanning.SetPlannedAction(
            player, 1, secondChaosGang ? GangAction.Chaos : GangAction.Hide);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
        match.FinishUpkeep();

        AiHandlerPass.Run(match, player);

        Assert.Equal(expected, match.AiPlanning.PlannedAction(player, 0));
    }

    // RULE-AI-023, FND-AI-049: Attack, Hide and Move share a branch, which raises Chaos in an
    // owned sector nobody raised Chaos in; Bribe, Research and Snitch plan nothing.
    [Theory]
    [InlineData(GangAction.Attack, GangAction.Chaos)]
    [InlineData(GangAction.Hide, GangAction.Chaos)]
    [InlineData(GangAction.Move, GangAction.Chaos)]
    [InlineData(GangAction.Snitch, GangAction.None)]
    [InlineData(GangAction.Research, GangAction.None)]
    [InlineData(GangAction.Bribe, GangAction.None)]
    public void JumpTableGroupsThePreviousActions(GangAction previous, GangAction expected)
    {
        var match = CreateMatch();
        var player = new PlayerId(0);
        BeginFamilyFourTurn(match);
        SetPreviousAction(match, previous);
        match.FinishUpkeep();

        AiHandlerPass.Run(match, player);

        Assert.Equal(expected, match.AiPlanning.PlannedAction(player, 0));
    }

    // RULE-AI-023, FND-AI-049: at weight 10 previous Hide takes the Attack/Move branch's single
    // draw, not the five draws of the Chaos/Equip branch.
    [Fact]
    public void PreviousHideMakesTheSingleDrawOfTheAttackBranch()
    {
        var match = CreateMatch(
            targetSector: 0, force: 8, targetForce: 10, weakAttacker: true);
        var player = new PlayerId(0);
        BeginFamilyFourTurn(match);
        SetPreviousAction(match, GangAction.Hide);
        match.FinishUpkeep();
        var randomBefore = match.Random.ConsumptionCount;

        AiHandlerPass.Run(match, player);

        Assert.Empty(AiTurnPlanner.Plan(match, player));
        Assert.Equal(3, match.Random.ConsumptionCount - randomBefore);
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
        Assert.Equal(GangAction.Chaos,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, match.Definitions);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match),
            MatchStateHasher.ComputeFingerprint(restored));
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
        match.AiPlanning.SeedFamily(player, 0, 4);
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
            scenario, GameDuration.SixMonths, 41, setups, MatchDeviations.Original,
            AiDifficulty.HomicidalManiac), players, sectors);
    }
}
