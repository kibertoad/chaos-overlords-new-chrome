namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static bool PrepareFamilyTwelveCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        var visible = VisibleOpponentsInSector(state, playerId, gang.SectorId);
        if (visible.Count == 0)
            PrepareFamilyTwelveUncontestedCommand(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
        else
            PrepareFamilyTwelveAttack(state, playerId, gang, gangSlot, visible);

        var turnsRemaining = Math.Max(0,
            ScenarioCatalog.Turns(state.Setup.Duration) - (state.Coordinator.Turn - 1));
        if (OriginalAiFamilyTwelveRules.ShouldTerminateForGreed(
                state.Setup.Scenario, turnsRemaining))
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Terminate);
        return true;
    }

    private static void PrepareFamilyTwelveUncontestedCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        var player = state.FindPlayer(playerId)!;
        if (OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
                state, player, gang, player.Cash) is { } weaponId
            && OriginalAiFamilyTwelveRules.CanEquip(
                state.AiPlanning.WeaponCooldown(playerId, gangSlot),
                state.Definitions.Items[weaponId].Cost,
                player.Cash))
        {
            SetFamilyTwelveEquipment(
                state, playerId, gangSlot, checked((short)weaponId),
                EquipmentSlot.Weapon);
            return;
        }

        if (OriginalAiEquipmentRules.SelectArmorUpgrade(
                state, player, gang, player.Cash) is { } armorId
            && OriginalAiFamilyTwelveRules.CanEquip(
                state.AiPlanning.ArmorCooldown(playerId, gangSlot),
                state.Definitions.Items[armorId].Cost,
                player.Cash))
        {
            SetFamilyTwelveEquipment(
                state, playerId, gangSlot, checked((short)armorId),
                EquipmentSlot.Armor);
            return;
        }

        if (OriginalAiEquipmentRules.SelectMiscellaneousChaosUpgrade(
                state, player, gang) is { } miscellaneousId
            && state.Definitions.Items[miscellaneousId].Cost <= player.Cash)
        {
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Equip,
                new AiActionTarget(checked((byte)miscellaneousId), 0));
            return;
        }

        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        if (OriginalAiFamilyTwelveRules.ShouldHeal(gang.Force, effectiveHeal))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            return;
        }

        var target = OriginalAiSectorSelectionRules.Select(
            mode: 0x40 + gang.SectorId,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 12,
            sectorOwners,
            sectorDisabled,
            sectorGangCounts,
            canSoloControl: _ => true,
            hasPriorChaos: _ => false,
            isHostileOwner: owner =>
                state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
            isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                .Setup.Controller == PlayerController.Human,
            playerOrder,
            state.Random);
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Move,
            new AiActionTarget(checked((byte)target), 0));
    }

    private static void SetFamilyTwelveEquipment(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        short itemId,
        EquipmentSlot slot)
    {
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Equip,
            new AiActionTarget(checked((byte)itemId), 0));
        state.AiPlanning.SetEquipmentCooldown(
            playerId, gangSlot, slot,
            OriginalAiFamilyTwelveRules.EquipmentCooldown(
                state.Definitions.Items[itemId].Cost));
    }

    private static void PrepareFamilyTwelveAttack(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible)
    {
        ObjectiveTarget selected = default;
        for (var attempt = 0; attempt < OriginalAiFamilyTwelveRules.AttackAttempts;
             attempt++)
        {
            var owner = state.Sectors[gang.SectorId].Owner;
            var targetPool = owner is { } sectorOwner
                && state.AiStrategy.IsHostile(playerId, sectorOwner)
                && VisibleOpponentWeight(state, playerId, sectorOwner) == 10
                    ? visible.Where(candidate => state.FindPlayer(candidate.Gang.Owner)?
                            .Setup.Controller == PlayerController.Human)
                        .ToArray()
                    : visible;
            var ordinal = state.Random.NextInclusive(targetPool.Count);
            selected = targetPool[ordinal - 1];
            var comparisonTarget = visible[ordinal - 1].Gang;
            var attackerStats = EffectiveStatisticsCalculator.ForGang(state, gang);
            var targetStats = EffectiveStatisticsCalculator.ForGang(state, comparisonTarget);
            if (OriginalAiFamilyTwelveRules.CanAttackSelectedTarget(
                    gang.Force, attackerStats.Combat, attackerStats.Defense,
                    comparisonTarget.Force, targetStats.Combat, targetStats.Defense))
                break;
        }

        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Attack,
            new AiActionTarget(
                checked((byte)selected.Gang.Owner.Value),
                checked((byte)selected.Slot)));
    }
}
