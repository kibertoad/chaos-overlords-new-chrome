namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static bool PrepareFamilyNineCommand(
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
                state, player, gang, player.Cash) is { } weaponId)
        {
            SetFamilyNineEquipment(
                state, playerId, gangSlot, checked((short)weaponId),
                EquipmentSlot.Weapon);
            return true;
        }

        if (OriginalAiEquipmentRules.SelectArmorUpgrade(
                state, player, gang, player.Cash) is { } armorId)
        {
            SetFamilyNineEquipment(
                state, playerId, gangSlot, checked((short)armorId),
                EquipmentSlot.Armor);
            return true;
        }

        var visible = VisibleOpponentsInSector(state, playerId, gang.SectorId);
        var visibleWeight = visible.Count == 0
            ? 0
            : VisibleOpponentWeight(state, playerId, visible[0].Gang.Owner);
        var action = OriginalAiFamilyNineRules.SelectTerritorialAction(
            state.Sectors[gang.SectorId].Owner == playerId,
            visibleWeight,
            state.AiPlanning.PreviousAction(playerId, gangSlot));
        if (action == GangAction.Move)
            PrepareFamilyNineMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
        else if (action == GangAction.Attack)
            PrepareFamilyNineAttack(state, playerId, gang, gangSlot, visible);
        else
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, action);
        return true;
    }

    private static void SetFamilyNineEquipment(
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
            OriginalAiFamilyNineRules.EquipmentCooldown(
                state.Definitions.Items[itemId].Cost));
    }

    private static void PrepareFamilyNineMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 3,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 9,
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

    private static void PrepareFamilyNineAttack(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible)
    {
        ObjectiveTarget selected = default;
        for (var attempt = 0; attempt < OriginalAiFamilyNineRules.AttackAttempts;
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
            var draw = DrawRecoveredAttackTarget(
                state, gang, visible, targetPool,
                OriginalAiFamilyNineRules.CanAttackSelectedTarget);
            selected = draw.Selected;
            if (draw.Accepted) break;
        }

        SetRecoveredAttackAction(state, playerId, gangSlot, selected);
    }
}
