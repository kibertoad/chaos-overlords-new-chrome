namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static bool PrepareFamilyFourCommand(
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
        var visibleWeight = visible.Count == 0
            ? 0
            : VisibleOpponentWeight(state, playerId, visible[0].Gang.Owner);
        var previousAction = state.AiPlanning.PreviousAction(playerId, gangSlot);
        switch (previousAction)
        {
            case GangAction.None:
            case GangAction.Control:
            case GangAction.Heal:
                PrepareFamilyFourHealHideOrMove(
                    state, playerId, gang, gangSlot,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
                break;
            case GangAction.Attack:
            case GangAction.Snitch:
            case GangAction.Move:
                PrepareFamilyFourAfterAttackSnitchOrMove(
                    state, playerId, gang, gangSlot, visible, visibleWeight,
                    previousAction,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
                break;
            case GangAction.Hide:
            case GangAction.Equip:
                PrepareFamilyFourAfterHideOrEquip(
                    state, playerId, gang, gangSlot, visible, visibleWeight,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
                break;
        }
        return true;
    }

    private static void PrepareFamilyFourHealHideOrMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        if (OriginalAiFamilyFourRules.ShouldHeal(
                gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Heal))
            SetFamilyFourAction(state, playerId, gangSlot, GangAction.Heal);
        else if (CountPreviousFamilyFourHidesInSector(
                     state, playerId, gang.SectorId) < 1)
            SetFamilyFourAction(state, playerId, gangSlot, GangAction.Hide);
        else
            PrepareFamilyFourMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
    }

    private static void PrepareFamilyFourAfterAttackSnitchOrMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible,
        int visibleWeight,
        GangAction previousAction,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        if (visibleWeight == 10)
        {
            var draw = DrawFamilyFourTarget(
                state, playerId, gang, visible, visibleWeight);
            if (draw.Accepted)
                SetRecoveredAttack(state, playerId, gang, gangSlot, draw.Selected);
            else
                ClearFamilyFourActionAndAuxiliaries(state, playerId, gangSlot);
            return;
        }

        if (state.Sectors[gang.SectorId].Owner == playerId)
        {
            if (CountPreviousFamilyFourHidesInSector(
                    state, playerId, gang.SectorId) < 1)
                SetFamilyFourAction(state, playerId, gangSlot, GangAction.Hide);
            else
                PrepareFamilyFourMove(
                    state, playerId, gang, gangSlot,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
            return;
        }

        if (OriginalAiFamilyFourRules.CanControlAfterRepeatedMove(
                previousAction,
                state.AiPlanning.OlderAction(playerId, gangSlot),
                CanSoloControl(state, playerId, gang)))
            SetFamilyFourAction(state, playerId, gangSlot, GangAction.Control);
        else
            PrepareFamilyFourMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
    }

    private static void PrepareFamilyFourAfterHideOrEquip(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible,
        int visibleWeight,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        if (visibleWeight == 10)
        {
            RecoveredAttackDraw draw = default;
            for (var attempt = 0;
                 attempt < OriginalAiFamilyFourRules.AttackAttemptsAfterHideOrEquip;
                 attempt++)
            {
                draw = DrawFamilyFourTarget(
                    state, playerId, gang, visible, visibleWeight);
                if (draw.Accepted) break;
            }
            SetRecoveredAttack(state, playerId, gang, gangSlot, draw.Selected);
        }

        var player = state.FindPlayer(playerId)!;
        if (state.AiPlanning.PlannedAction(playerId, gangSlot) != GangAction.Attack
            && OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                state, player, gang, gangSlot) is { } upgrade)
            SetFamilyFourEquipment(state, playerId, gangSlot, upgrade);

        if (state.AiPlanning.PlannedAction(playerId, gangSlot)
            is GangAction.Attack or GangAction.Equip)
            return;

        if (state.Sectors[gang.SectorId].Owner == playerId
            && CountPreviousFamilyFourHidesInSector(
                state, playerId, gang.SectorId)
                <= OriginalAiFamilyFourRules.MaximumPreviousHidesInOwnedSector)
            SetFamilyFourAction(state, playerId, gangSlot, GangAction.Hide);
        else
            PrepareFamilyFourMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
    }

    private static RecoveredAttackDraw DrawFamilyFourTarget(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        IReadOnlyList<ObjectiveTarget> visible,
        int visibleWeight)
    {
        var targetPool = SelectHumanWeightedTargetPool(
            state, playerId, gang.SectorId, visible, visibleWeight);
        return DrawRecoveredAttackTarget(
            state, gang, visible, targetPool,
            OriginalAiFamilyFourRules.CanAttackSelectedTarget);
    }

    private static void SetFamilyFourEquipment(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        OriginalAiEquipmentRules.Upgrade upgrade)
    {
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Equip,
            new AiActionTarget(checked((byte)upgrade.ItemId), 0));
        state.AiPlanning.SetEquipmentCooldown(
            playerId, gangSlot, upgrade.Slot,
            OriginalAiEquipmentRules.EquipmentReplacementCooldown(
                state.Definitions.Items[upgrade.ItemId].Cost));
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }

    private static void SetFamilyFourAction(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        GangAction action)
    {
        state.AiPlanning.SetPlannedAction(playerId, gangSlot, action);
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }

    private static void ClearFamilyFourActionAndAuxiliaries(
        MatchState state,
        PlayerId playerId,
        int gangSlot)
    {
        state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.None);
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
        state.AiPlanning.SetCoverageSector(
            playerId, gangSlot, AiPlanningState.InactiveCoverageSector);
    }

    private static void PrepareFamilyFourMove(
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
            mode: 2,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 4,
            sectorOwners,
            sectorDisabled,
            sectorGangCounts,
            canSoloControl: sectorId => CanSoloControl(state, playerId, gang, sectorId),
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
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }

    private static int CountPreviousFamilyFourHidesInSector(
        MatchState state,
        PlayerId playerId,
        int sectorId)
    {
        var player = state.FindPlayer(playerId)!;
        return player.Gangs.Select((gang, slot) => (gang, slot))
            .Count(entry => entry.gang.IsActive
                && entry.gang.SectorId == sectorId
                && state.AiPlanning.PreviousAction(playerId, entry.slot)
                    == GangAction.Hide);
    }
}
