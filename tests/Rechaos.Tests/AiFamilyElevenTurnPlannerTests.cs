using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiFamilyElevenTurnPlannerTests
{
    [Fact]
    public void PreparationEquipsExactRecoveredWeaponAndStartsCostCooldown()
    {
        var data = BundledOriginalData.Load();
        var researched = data.Items
            .Where(item => item.Type != 99)
            .Select(item => item.Id)
            .ToHashSet();
        var match = CreateMatch(data, definitionId: 56, force: 10,
            ownsSector: true, rivalInSector: false,
            researchedItems: researched, cash: 500);
        var player = new PlayerId(0);
        BeginFamilyElevenTurn(match, player);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();
        var expected = Assert.IsType<OriginalAiEquipmentRules.Upgrade>(
            OriginalAiEquipmentRules.SelectFamilyElevenUpgrade(
                match, match.Players[0], match.Players[0].Gangs[0], 0));
        Assert.Equal(EquipmentSlot.Weapon, expected.Slot);

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(11, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Equip, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(CommandTarget.Item(expected.ItemId), command.Target);
        Assert.Equal(OriginalAiEquipmentRules.EquipmentReplacementCooldown(
                data.Items[expected.ItemId].Cost),
            expected.Slot == EquipmentSlot.Weapon
                ? match.AiPlanning.WeaponCooldown(player, 0)
                : match.AiPlanning.ArmorCooldown(player, 0));
    }

    [Fact]
    public void PreparationHealsBelowForceEightWhenNoEquipmentIsAvailable()
    {
        var data = BundledOriginalData.Load();
        var definition = data.Gangs.First(gang => gang.Stats.Heal >= -3);
        var match = CreateMatch(data, definition.Id, force: 7,
            ownsSector: true, rivalInSector: false,
            researchedItems: new HashSet<short>(), cash: 20);
        var player = new PlayerId(0);
        BeginFamilyElevenTurn(match, player);
        match.FinishUpkeep();

        match.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.Equal(11, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Heal, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(GangAction.Heal, command.Action);
    }

    [Fact]
    public void PreparationAttacksFirstVisibleLocalGangAndReplays()
    {
        var data = BundledOriginalData.Load();
        var definition = data.Gangs
            .OrderByDescending(gang => gang.Stats.Detect)
            .ThenByDescending(gang => gang.Stats.Combat)
            .First();
        var match = CreateMatch(data, definition.Id, force: 10,
            ownsSector: false, rivalInSector: true,
            researchedItems: new HashSet<short>(), cash: 20);
        var player = new PlayerId(0);
        BeginFamilyElevenTurn(match, player);
        var recorder = new MatchReplayRecorder(match);
        recorder.FinishUpkeep();

        recorder.PrepareAiPlanning(player);
        var command = Assert.Single(AiTurnPlanner.Plan(match, player));

        Assert.True(match.CanPlayerDetectGang(player, new GangId(20)));
        Assert.Equal(11, match.AiPlanning.Family(player, 0));
        Assert.Equal(GangAction.Attack, match.AiPlanning.PlannedAction(player, 0));
        Assert.Equal(new AiActionTarget(1, 0), match.AiPlanning.PlannedTarget(player, 0));
        Assert.Equal(GangAction.Attack, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(20)), command.Target);

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
    }

    private static void BeginFamilyElevenTurn(MatchState match, PlayerId player)
    {
        match.AiPlanning.BeginPlanning(player);
        match.AiPlanning.SetCurrentHireRole(player, 3);
    }

    private static MatchState CreateMatch(
        OriginalData data,
        short definitionId,
        int force,
        bool ownsSector,
        bool rivalInSector,
        IReadOnlySet<short> researchedItems,
        int cash)
    {
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], cash,
                [new MatchGangState(new GangId(10), setups[0].Id, definitionId, 0, force)],
                researchedItems: researchedItems),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), setups[1].Id, 2,
                    rivalInSector ? 0 : 1, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: ownsSector && id == 0 ? setups[0].Id : null, income: 3))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            ScenarioId.Siege, GameDuration.SixMonths, 37, setups), players, sectors);
    }
}
