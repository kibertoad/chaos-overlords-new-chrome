using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyNineTurnPlannerTests
{
    [Fact]
    public void WeaponUpgradeIgnoresExistingCooldownOverwritesItAndReplays()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Select((item, index) => (item, index))
            .Where(value => value.item.Type != 99)
            .Select(value => checked((short)value.index));
        var match = CreateMatch(data, cash: 500, researched: researched,
            attackerDefinitionId: 5);
        var player = new PlayerId(0);
        BeginFamilyNineTurn(match, player);
        match.AiPlanning.SetEquipmentCooldown(player, 0, EquipmentSlot.Weapon, 99);
        var expectedItem = Assert.IsType<int>(
            OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
                match, match.Players[0], match.Players[0].Gangs[0], 500));
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Equip, command.Action);
        Assert.Equal(CommandTarget.Item(checked((short)expectedItem)), command.Target);
        Assert.Equal(data.Items[expectedItem].Cost * 3,
            match.AiPlanning.WeaponCooldown(player, 0));

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
    public void OwnedSectorMovesThroughModeThreeTowardOpponentTerritory()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, researched: [],
            targetSector: 1, targetSectorOwner: new PlayerId(1));
        var player = new PlayerId(0);
        BeginFamilyNineTurn(match, player);
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Move, command.Action);
        Assert.Equal(CommandTarget.Sector(1), command.Target);
        Assert.Equal(0, match.Random.ConsumptionCount);
    }

    [Fact]
    public void UnownedSectorWithoutWeightTenOpponentChoosesControl()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, researched: [],
            sourceOwner: new PlayerId(1));
        var player = new PlayerId(0);
        BeginFamilyNineTurn(match, player);
        match.FinishUpkeep();

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);

        Assert.Equal(GangAction.Control,
            Assert.Single(AiTurnPlanner.Plan(match, player)).Action);
    }

    [Fact]
    public void FiveFailedComparisonsStillAttackFinalVisibleTarget()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(data, cash: 20, researched: [], force: 1,
            attackerDefinitionId: 1, targetDefinitionId: 4,
            targetSector: 0, sourceOwner: new PlayerId(1),
            mentality: AiDifficulty.HomicidalManiac);
        var player = new PlayerId(0);
        BeginFamilyNineTurn(match, player);
        match.FinishUpkeep();
        var attacker = match.Players[0].Gangs[0];
        var target = match.Players[1].Gangs[0];
        var attackerStats = EffectiveStatisticsCalculator.ForGang(match, attacker);
        var targetStats = EffectiveStatisticsCalculator.ForGang(match, target);
        Assert.True(match.CanPlayerDetectGang(player, target.Id));
        Assert.False(OriginalAiFamilyNineRules.CanAttackSelectedTarget(
            attacker.Force, attackerStats.Combat, attackerStats.Defense,
            target.Force, targetStats.Combat, targetStats.Defense));

        AiTurnPlanner.PrepareRecoveredFamilyCommands(match, player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(target.Id), command.Target);
        Assert.Equal(15, match.Random.ConsumptionCount);
    }

    private static void BeginFamilyNineTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetFamily(player, 0, 9);
        match.AiPlanning.SetCurrentHireRole(player, 5);
    }

    private static MatchState CreateMatch(
        OriginalData data,
        int cash,
        IEnumerable<short> researched,
        int force = 10,
        short attackerDefinitionId = 0,
        short targetDefinitionId = 2,
        int targetSector = 63,
        PlayerId? sourceOwner = null,
        PlayerId? targetSectorOwner = null,
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
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 0),
                new MatchSiteState(1, 3, 0),
                new MatchSiteState(2, 5, 0)
            ], owner: id == 0
                    ? sourceOwner ?? setups[0].Id
                    : id == targetSector ? targetSectorOwner : null,
                income: 0))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Siege, GameDuration.SixMonths, 41, setups, mentality),
            players, sectors);
    }
}
