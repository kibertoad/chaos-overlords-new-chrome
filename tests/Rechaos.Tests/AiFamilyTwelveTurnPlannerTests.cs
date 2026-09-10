using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyTwelveTurnPlannerTests
{
    [Fact]
    public void WeaponUpgradeHasPriorityUsesRawCooldownAndReplays()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type != 99)
            .Select(value => checked((short)value.index));
        var match = CreateMatch(data, cash: 500, force: 10,
            researched: researched, attackerDefinitionId: 5);
        var player = new PlayerId(0);
        BeginFamilyTwelveTurn(match, player);
        var expectedItem = Assert.IsType<int>(
            OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
                match, match.Players[0], match.Players[0].Gangs[0], 500));
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(12, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(checked((short)expectedItem)), command.Target);
        Assert.Equal(data.Items[expectedItem].Cost,
            match.AiPlanning.WeaponCooldown(player, 0));

        Assert.True(recorder.Submit(command).Accepted);
        recorder.FinishCommand(player);
        recorder.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution)
            recorder.FinishExecutionPhase();
        Assert.Equal((short)expectedItem, match.Players[0].Gangs[0].WeaponItemId);

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(MatchStateHasher.ComputeSha256(match),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void ArmorUpgradeFollowsUnavailableWeapon()
    {
        var data = BundledOriginalData.Load();
        var researched = Enumerable.Range(24, 14).Select(value => (short)value);
        var match = CreateMatch(data, cash: 100, force: 10,
            researched: researched);
        var player = new PlayerId(0);
        BeginFamilyTwelveTurn(match, player);
        match.AiPlanning.SetEquipmentCooldown(player, 0, EquipmentSlot.Weapon, 1);
        var expectedItem = Assert.IsType<int>(OriginalAiEquipmentRules.SelectArmorUpgrade(
            match, match.Players[0], match.Players[0].Gangs[0], 100));
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(checked((short)expectedItem)), command.Target);
        Assert.Equal(data.Items[expectedItem].Cost,
            match.AiPlanning.ArmorCooldown(player, 0));
    }

    [Fact]
    public void MiscellaneousChaosUpgradeFollowsUnavailableEquipmentSlots()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 500, force: 10,
            researched: [40], attackerDefinitionId: 5);
        var player = new PlayerId(0);
        BeginFamilyTwelveTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(40), command.Target);
        Assert.Equal(0, match.AiPlanning.WeaponCooldown(player, 0));
        Assert.Equal(0, match.AiPlanning.ArmorCooldown(player, 0));
    }

    [Fact]
    public void LowForceGangHealsBeforeMoving()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, force: 9, researched: []);
        var player = new PlayerId(0);
        BeginFamilyTwelveTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.Heal,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void EncodedCurrentSectorModeUsesZeroMaximumFallbackStep()
    {
        const int source = 20;
        var seed = Enumerable.Range(1, 1_000).First(candidate =>
        {
            var random = new DeterministicRandom(candidate);
            return random.NextInclusive(MatchLimits.SectorCount) - 1 != source;
        });
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, force: 10, researched: [],
            seed: seed, attackerSector: source);
        var player = new PlayerId(0);
        BeginFamilyTwelveTurn(match, player);
        match.FinishUpkeep();
        var expectedRandom = new DeterministicRandom(seed);
        var sampled = expectedRandom.NextInclusive(MatchLimits.SectorCount) - 1;
        var expected = StepToward(source, sampled);

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(expected), command.Target);
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void FiveFailedComparisonsStillAttackFinalTarget()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, force: 1, researched: [],
            attackerDefinitionId: 1, targetDefinitionId: 4,
            targetSector: 0, sectorOwner: new PlayerId(1),
            mentality: AiDifficulty.HomicidalManiac);
        var player = new PlayerId(0);
        BeginFamilyTwelveTurn(match, player);
        match.FinishUpkeep();
        var attacker = match.Players[0].Gangs[0];
        var target = match.Players[1].Gangs[0];
        var attackerStats = EffectiveStatisticsCalculator.ForGang(match, attacker);
        var targetStats = EffectiveStatisticsCalculator.ForGang(match, target);
        Assert.True(match.CanPlayerDetectGang(player, target.Id));
        Assert.False(OriginalAiFamilyTwelveRules.CanAttackSelectedTarget(
            attacker.Force, attackerStats.Combat, attackerStats.Defense,
            target.Force, targetStats.Combat, targetStats.Defense));

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(target.Id), command.Target);
        Assert.Equal(15, match.Random.ConsumptionCount);
    }

    [Fact]
    public void PassingComparisonStopsAfterFirstTargetDraw()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, force: 10, researched: [],
            attackerDefinitionId: 4, targetDefinitionId: 2,
            targetSector: 0, sectorOwner: new PlayerId(1),
            mentality: AiDifficulty.HomicidalManiac);
        var player = new PlayerId(0);
        BeginFamilyTwelveTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void EmptyHumanOnlyPoolDegradesToNoActionWithoutThrowing()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "HUMAN OWNER", PlayerController.Human),
            new(new PlayerId(2), "VISIBLE CPU", PlayerController.Computer)
        ];
        var attackerDefinition = data.Gangs
            .OrderByDescending(gang => gang.Stats.Detect)
            .First();
        var visibleDefinition = data.Gangs
            .OrderBy(gang => gang.Stats.Stealth)
            .First();
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), setups[0].Id,
                    attackerDefinition.Id, 0, 10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, 63, 10)]),
            new(setups[2], 20,
                [new MatchGangState(new GangId(30), setups[2].Id,
                    visibleDefinition.Id, 0, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 5, 15)
            ], owner: id == 0 ? setups[1].Id : null, income: 3))
            .ToArray();
        var match = new MatchState(data, new MatchSetup(
            ScenarioId.Siege, GameDuration.SixMonths, 41, setups,
            AiDifficulty.HomicidalManiac), players, sectors);
        var player = new PlayerId(0);
        BeginFamilyTwelveTurn(match, player);
        match.FinishUpkeep();
        Assert.True(match.CanPlayerDetectGang(player, new GangId(30)));
        Assert.False(match.CanPlayerDetectGang(player, new GangId(20)));

        match.PrepareAiPlanning(player);

        Assert.Equal(GangAction.None, match.AiPlanning.PlannedAction(player, 0));
        Assert.Empty(AiTurnPlanner.Plan(match, player));
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void FinalThreeGreedTurnsOverridePreparedActionWithTerminate()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, force: 10, researched: [],
            scenario: ScenarioId.Greed);
        var player = new PlayerId(0);
        AdvanceCoordinatorToTurn(match.Coordinator, 24, match.Players.Count);
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 12);
        match.Coordinator.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Terminate,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    private static void BeginFamilyTwelveTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 4);
    }

    private static int StepToward(int source, int target)
    {
        var result = source;
        var sourceX = source % MatchLimits.BoardWidth;
        var sourceY = source / MatchLimits.BoardWidth;
        var targetX = target % MatchLimits.BoardWidth;
        var targetY = target / MatchLimits.BoardWidth;
        if (sourceX < targetX) result++;
        if (sourceX > targetX) result--;
        if (sourceY < targetY) result += MatchLimits.BoardWidth;
        if (sourceY > targetY) result -= MatchLimits.BoardWidth;
        return result;
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
        int cash,
        int force,
        IEnumerable<short> researched,
        short attackerDefinitionId = 0,
        short targetDefinitionId = 2,
        int seed = 41,
        int attackerSector = 0,
        int targetSector = 63,
        PlayerId? sectorOwner = null,
        ScenarioId scenario = ScenarioId.Siege,
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
                    attackerDefinitionId, attackerSector, force)],
                researchedItems: researched.ToHashSet()),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id,
                    targetDefinitionId, targetSector, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 5, 15)
            ], owner: id == attackerSector ? sectorOwner ?? setups[0].Id : null,
                income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, seed, setups, mentality),
            players, sectors);
    }
}
