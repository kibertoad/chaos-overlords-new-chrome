using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiObjectiveTurnPlannerTests
{
    [Theory]
    [InlineData(1, 13)]
    [InlineData(2, 14)]
    public void OwnedObjectiveEquipsRecoveredWeaponWithFixedCooldown(
        int hireRole,
        int expectedFamily)
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Where(item => item.Type != 99)
            .Select(item => item.Id)
            .ToHashSet();
        var match = CreateMatch(data, definitionId: 5, cash: 500, researched);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, hireRole);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();
        var expectedItem = Assert.IsType<int>(
            OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
                match, match.Players[0], match.Players[0].Gangs[0],
                match.Players[0].Cash));

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(expectedFamily, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Equip, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(checked((byte)expectedItem), 0),
            match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(OriginalAiObjectiveFamilyRules.ObjectiveEquipmentCooldown,
            match.AiPlanning.WeaponCooldown(player, 0));
        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(expectedItem), command.Target);
    }

    [Theory]
    [InlineData(1, 13)]
    [InlineData(2, 14)]
    public void OwnedObjectiveInfluencesExactHighestSupportUnfinishedSiteAndReplays(
        int hireRole,
        int expectedFamily)
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(
            data, definitionId: 4, cash: 20, new HashSet<short>());
        var player = new PlayerId(0);
        match.Sectors[27].Sites[0].Resistance = 0;
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, hireRole);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(expectedFamily, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Influence, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(1, 0), match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(GangAction.Influence, command.Action);
        Assert.Equal(CommandTarget.Site(27 * MatchLimits.SitesPerSector + 1), command.Target);

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

    [Theory]
    [InlineData(1, 13)]
    [InlineData(2, 14)]
    public void OwnedObjectiveWithVisibleOpponentUsesRecoveredAttack(
        int hireRole,
        int expectedFamily)
    {
        var data = BundledOriginalData.Load();
        var attacker = data.Gangs
            .OrderByDescending(gang => gang.Stats.Detect)
            .ThenByDescending(gang => gang.Stats.Combat)
            .First();
        var match = CreateMatch(
            data, attacker.Id, cash: 20, new HashSet<short>());
        var player = new PlayerId(0);
        match.Players[1].Gangs[0].SectorId = 27;
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, hireRole);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.True(match.CanPlayerDetectGang(player, new GangId(20)));
        Assert.Equal(expectedFamily, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Attack, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(1, 0), match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
    }

    [Theory]
    [InlineData(1, 13)]
    [InlineData(2, 14)]
    public void OwnedObjectiveWithoutUpgradeOrUnfinishedSitePreservesNoAction(
        int hireRole,
        int expectedFamily)
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(
            data, definitionId: 4, cash: 20, new HashSet<short>());
        foreach (var site in match.Sectors[27].Sites)
            site.Resistance = 0;
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, hireRole);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Empty(AiTurnPlanner.Plan(match, player));
        Assert.Equal(expectedFamily, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.None, match.AiPlanning.PlannedAction(player, 0));
    }

    // RULE-AI-031, FND-AI-062: on an objective another player owns, the pool is that owner's
    // visible gangs. With none there no draw is made, and a gang at Force 10 that fails the Heal
    // test gets no write, where FND-AI-039 read Control.
    [Theory]
    [InlineData(1, 13, false)]
    [InlineData(2, 14, false)]
    [InlineData(1, 13, true)]
    [InlineData(2, 14, true)]
    public void ContestedObjectiveWithAnEmptyPoolAndNoHealWritesNothing(
        int hireRole,
        int expectedFamily,
        bool thirdTurn)
    {
        var data = BundledOriginalData.Load();
        var attacker = data.Gangs
            .OrderByDescending(gang => gang.Stats.Detect)
            .ThenByDescending(gang => gang.Stats.Combat)
            .First();
        var match = CreateMatch(
            data, attacker.Id, cash: 20, new HashSet<short>(), thirdPlayerOwnsObjective: true);
        var player = new PlayerId(0);
        match.Players[1].Gangs[0].SectorId = 27;
        if (thirdTurn) AdvanceTurn(match);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, hireRole);
        match.FinishUpkeep();
        var turnsRemaining = ScenarioCatalog.Turns(match.Setup.Duration)
            - (match.Coordinator.Turn - 1);

        match.PrepareAiPlanning(player);

        Assert.True(match.CanPlayerDetectGang(player, new GangId(20)));
        Assert.Equal(expectedFamily, match.AiPlanning.Family(player, 0));
        Assert.Equal(turnsRemaining % 2 == 0 ? GangAction.None : GangAction.Control,
            match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(AiPlanningState.InactiveFocusValue, match.AiPlanning.FocusValue(player, 0));
    }

    // RULE-AI-002: on the first turn of Big Man a gang given a family takes hire role 1's family.
    [Fact]
    public void FirstBigManTurnForcesHireRoleOne()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(
            data, definitionId: 4, cash: 20, new HashSet<short>(), secondTurn: false);
        var player = new PlayerId(0);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 2);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        AiTurnPlanner.Plan(match, player);

        Assert.Equal(1, match.AiPlanning.CurrentHireRole(player));
        Assert.Equal(13, match.AiPlanning.Family(player, 0));
    }

    private static MatchState CreateMatch(
        OriginalData data,
        short definitionId,
        int cash,
        IReadOnlySet<short> researchedItems,
        bool secondTurn = true,
        bool thirdPlayerOwnsObjective = false)
    {
        MatchPlayerSetup[] setups = thirdPlayerOwnsObjective
            ?
            [
                new(new PlayerId(0), "CPU", PlayerController.Computer),
                new(new PlayerId(1), "RIVAL", PlayerController.Human),
                new(new PlayerId(2), "OWNER", PlayerController.Computer)
            ]
            :
            [
                new(new PlayerId(0), "CPU", PlayerController.Computer),
                new(new PlayerId(1), "RIVAL", PlayerController.Human)
            ];
        MatchPlayerState[] players =
        [
            new(setups[0], cash,
                [new MatchGangState(new GangId(10), setups[0].Id, definitionId, 27, 10)],
                researchedItems: researchedItems),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, 1, 10)]),
            .. thirdPlayerOwnsObjective
                ? new[]
                {
                    new MatchPlayerState(setups[2], 20,
                        [new MatchGangState(new GangId(30), setups[2].Id, 3, 63, 10)])
                }
                : []
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 27 ? setups[thirdPlayerOwnsObjective ? 2 : 0].Id : null,
                income: 3))
            .ToArray();
        var match = new MatchState(data, new MatchSetup(
            ScenarioId.BigMan, GameDuration.SixMonths, 31, setups), players, sectors);
        // RULE-AI-002 forces hire role 1 on the first Big Man turn, so the role cases play turn 2.
        if (secondTurn) AdvanceTurn(match);
        return match;
    }

    private static void AdvanceTurn(MatchState match)
    {
        var coordinator = match.Coordinator;
        coordinator.FinishUpkeep();
        foreach (var setup in match.Setup.Players) coordinator.FinishCommand(setup.Id);
        foreach (var _ in TurnStructure.ExecutionOrder) coordinator.FinishExecutionPhase();
        foreach (var setup in match.Setup.Players) coordinator.FinishHire(setup.Id);
        coordinator.FinishPlayerElimination();
    }
}
