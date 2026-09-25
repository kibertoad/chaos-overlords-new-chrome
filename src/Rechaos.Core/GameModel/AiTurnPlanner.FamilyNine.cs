namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static void PrepareFamilyNineCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var player = state.FindPlayer(playerId)!;
        if (OriginalAiEquipmentRules.SelectFamily11WeaponUpgrade(
                state, player, gang, player.Cash) is { } weaponId)
        {
            SetFamilyNineEquipment(
                state, playerId, gangSlot, checked((short)weaponId),
                EquipmentSlot.Weapon);
            return;
        }

        if (OriginalAiEquipmentRules.SelectArmorUpgrade(
                state, player, gang, player.Cash) is { } armorId)
        {
            SetFamilyNineEquipment(
                state, playerId, gangSlot, checked((short)armorId),
                EquipmentSlot.Armor);
            return;
        }

        var visible = VisibleOpponentsInSector(state, playerId, gang.SectorId);
        var visibleWeight = FirstVisibleOpponentWeight(state, playerId, visible);
        var action = OriginalAiFamilyNineRules.SelectTerritorialAction(
            state.Sectors[gang.SectorId].Owner == playerId,
            visibleWeight,
            state.AiPlanning.PreviousAction(playerId, gangSlot));
        if (action == GangAction.Move)
            PrepareFamilyNineMove(
                state, playerId, gang, gangSlot, snapshot);
        else if (action == GangAction.Attack)
            PrepareFamilyNineAttack(state, playerId, gang, gangSlot, visible);
        else
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, action);
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
        FamilyPlanningSnapshot snapshot)
    {
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 3,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 9,
            snapshot.SectorOwners,
            snapshot.SectorDisabled,
            snapshot.SectorGangCounts,
            canSoloControl: _ => true,
            hasPriorChaos: _ => false,
            isHostileOwner: owner =>
                state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
            isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                .Setup.Controller == PlayerController.Human,
            snapshot.PlayerOrder,
            state.Random);
        SetRecoveredMoveAction(state, playerId, gangSlot, target);
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
            // RULE-AI-004 hostile_human_owner: the owner byte, then hostile_owner's query.
            var targetPool = owner is { } sectorOwner
                && IsHostileOwner(state, playerId, gang.SectorId)
                && state.FindPlayer(sectorOwner)?.Setup.Controller == PlayerController.Human
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
