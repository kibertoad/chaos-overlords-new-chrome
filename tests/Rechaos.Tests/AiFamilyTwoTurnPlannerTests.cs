using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyTwoTurnPlannerTests
{
    [Fact]
    public void ArmorUpgradePrecedesWeaponUsesCostScaledCooldownAndReplays()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type != 99)
            .Select(value => checked((short)value.index));
        var match = CreateMatch(data, researched: researched, cash: 500,
            attackerDefinitionId: 5);
        var player = new PlayerId(0);
        BeginFamilyTwoTurn(match, player);
        var expectedArmor = Assert.IsType<int>(OriginalAiEquipmentRules.SelectArmorUpgrade(
            match, match.Players[0], match.Players[0].Gangs[0], 500));
        Assert.NotNull(OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
            match, match.Players[0], match.Players[0].Gangs[0], 500));
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(2, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(checked((short)expectedArmor)), command.Target);
        Assert.Equal(data.Items[expectedArmor].Cost * 3,
            match.AiPlanning.ArmorCooldown(player, 0));
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
    public void WeaponUpgradeFollowsUnavailableArmor()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type is 0 or 1 or 2)
            .Select(value => checked((short)value.index));
        var match = CreateMatch(data, researched: researched, cash: 500,
            attackerDefinitionId: 5);
        var player = new PlayerId(0);
        BeginFamilyTwoTurn(match, player);
        var expectedWeapon = Assert.IsType<int>(
            OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
                match, match.Players[0], match.Players[0].Gangs[0], 500));
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(checked((short)expectedWeapon)), command.Target);
        Assert.Equal(data.Items[expectedWeapon].Cost * 3,
            match.AiPlanning.WeaponCooldown(player, 0));
    }

    [Fact]
    public void PreviousAttackBlocksBothEquipmentChoices()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type != 99)
            .Select(value => checked((short)value.index));
        var match = CreateMatch(data, researched: researched, cash: 500,
            attackerDefinitionId: 5);
        var player = new PlayerId(0);
        BeginFamilyTwoTurn(match, player);
        SetPreviousAction(match, player, GangAction.Attack);
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Move,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void LowForceGangHealsWithoutWeightTenOpponent()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, researched: [], cash: 20, force: 7);
        var player = new PlayerId(0);
        BeginFamilyTwoTurn(match, player);
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void OwnedSectorMovesTowardUniqueScenarioLeaderThroughModeSix()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, researched: [], cash: 20,
            rivalOwnedSectors: [1, 2]);
        var player = new PlayerId(0);
        BeginFamilyTwoTurn(match, player);
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(1), command.Target);
        Assert.Equal(0, match.Random.ConsumptionCount);
    }

    [Fact]
    public void FiveFailedHostileComparisonsStillAttackAndStoreSectorFocus()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, researched: [], cash: 20, force: 1,
            attackerDefinitionId: 1, targetDefinitionId: 4,
            sourceOwner: new PlayerId(1), targetSector: 0,
            mentality: AiDifficulty.HomicidalManiac);
        var player = new PlayerId(0);
        BeginFamilyTwoTurn(match, player);
        match.FinishUpkeep();
        var attacker = match.Players[0].Gangs[0];
        var target = match.Players[1].Gangs[0];
        var attackerStats = EffectiveStatisticsCalculator.ForGang(match, attacker);
        var targetStats = EffectiveStatisticsCalculator.ForGang(match, target);
        Assert.False(OriginalAiFamilyTwoRules.CanAttackSelectedTarget(
            attacker.Force, attackerStats.Combat, attackerStats.Defense,
            target.Force, targetStats.Combat, targetStats.Defense));

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(target.Id), command.Target);
        Assert.Equal(0, match.AiPlanning.FocusValue(player, 0));
        Assert.Equal(15, match.Random.ConsumptionCount);
    }

    [Fact]
    public void ControllableNonOwnedSectorControlsThenMovesAfterPreviousControl()
    {
        var data = BundledOriginalData.Load();
        var control = CreateMatch(data, researched: [], cash: 20,
            sourceOwner: new PlayerId(1), sourceIncome: 0);
        var move = CreateMatch(data, researched: [], cash: 20,
            sourceOwner: new PlayerId(1), sourceIncome: 0);
        var player = new PlayerId(0);
        BeginFamilyTwoTurn(control, player);
        BeginFamilyTwoTurn(move, player);
        SetPreviousAction(move, player, GangAction.Control);
        control.FinishUpkeep();
        move.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(control, player);
        AiTurnPlanner.PrepareRecoveredFamilyCommands(move, player);

        Assert.Equal(GangAction.Control,
            Assert.Single(AiTurnPlanner.Plan(control, player)).Action);
        Assert.Equal(GangAction.Move,
            Assert.Single(AiTurnPlanner.Plan(move, player)).Action);
    }

    [Fact]
    public void HostileHumanOwnerWithoutVisibleHumanGangForcesLateControl()
    {
        var data = BundledOriginalData.Load();
        var match = CreateSparseThreePlayerMatch(data, humanOwner: true,
            mentality: AiDifficulty.HomicidalManiac);
        var player = new PlayerId(0);
        BeginFamilyTwoTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.Control,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void CombatAdvantageFlagForcesControlWithoutVisibleOwnerGang()
    {
        var data = BundledOriginalData.Load();
        var match = CreateSparseThreePlayerMatch(data, humanOwner: false,
            mentality: AiDifficulty.Criminal);
        var player = new PlayerId(0);
        BeginFamilyTwoTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.True(match.AiStrategy.IsHostile(player, new PlayerId(1)));
        Assert.Equal(GangAction.Control,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void FinalThreeGreedTurnsOverridePreparedActionWithTerminate()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, researched: [], cash: 20,
            scenario: ScenarioId.Greed);
        var player = new PlayerId(0);
        AdvanceCoordinatorToTurn(match.Coordinator, 24, match.Players.Count);
        BeginFamilyTwoTurn(match, player);
        match.Coordinator.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Terminate,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    private static void BeginFamilyTwoTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 2);
        match.AiPlanning.SetCurrentHireRole(player, 3);
    }

    private static void SetPreviousAction(
        MatchState match,
        PlayerId player,
        GangAction action)
    {
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
        IEnumerable<short> researched,
        int cash,
        int force = 10,
        short attackerDefinitionId = 0,
        short targetDefinitionId = 2,
        PlayerId? sourceOwner = null,
        int sourceIncome = 0,
        int targetSector = 63,
        IReadOnlyList<int>? rivalOwnedSectors = null,
        ScenarioId scenario = ScenarioId.Power,
        AiDifficulty mentality = AiDifficulty.Criminal)
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
                    attackerDefinitionId, 0, force)],
                researchedItems: researched.ToHashSet()),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id,
                    targetDefinitionId, targetSector, 10)])
        ];
        var rivalSectors = rivalOwnedSectors?.ToHashSet() ?? [];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 5, 15)
            ], owner: id == 0
                    ? sourceOwner ?? setups[0].Id
                    : rivalSectors.Contains(id) ? setups[1].Id : null,
                income: id == 0 ? sourceIncome : 0))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, 41, setups, mentality), players, sectors);
    }

    private static MatchState CreateSparseThreePlayerMatch(
        OriginalData data,
        bool humanOwner,
        AiDifficulty mentality)
    {
        var strong = data.Gangs
            .OrderByDescending(gang => gang.Stats.Combat + gang.Stats.Defense)
            .First();
        var visible = data.Gangs.OrderBy(gang => gang.Stats.Stealth).First();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "OWNER",
                humanOwner ? PlayerController.Human : PlayerController.Computer),
            new(new PlayerId(2), "VISIBLE CPU", PlayerController.Computer)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id, strong.Id, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, visible.Id, 63, 10)]),
            new(setups[2], 20,
                [new MatchGangState(new GangId(30), setups[2].Id, visible.Id, 0, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 5, 15)
            ], owner: id == 0 ? setups[1].Id : null,
                income: id == 0 ? 100 : 0))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, 41, setups, mentality),
            players, sectors);
    }
}
