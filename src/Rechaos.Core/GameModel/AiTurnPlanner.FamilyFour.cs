namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static void PrepareFamilyFourCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var visible = VisibleOpponentsInSector(state, playerId, gang.SectorId);
        var visibleWeight = FirstVisibleOpponentWeight(state, playerId, visible);
        var previousAction = state.AiPlanning.PreviousAction(playerId, gangSlot);
        switch (previousAction)
        {
            case GangAction.None:
            case GangAction.Control:
            case GangAction.Heal:
                PrepareFamilyFourHealHideOrMove(
                    state, playerId, gang, gangSlot, snapshot);
                break;
            case GangAction.Attack:
            case GangAction.Snitch:
            case GangAction.Move:
                PrepareFamilyFourAfterAttackSnitchOrMove(
                    state, playerId, gang, gangSlot, visible, visibleWeight,
                    previousAction, snapshot);
                break;
            case GangAction.Hide:
            case GangAction.Equip:
                PrepareFamilyFourAfterHideOrEquip(
                    state, playerId, gang, gangSlot, visible, visibleWeight, snapshot);
                break;
        }
    }

    private static void PrepareFamilyFourHealHideOrMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        if (OriginalAiFamilyFourRules.ShouldHeal(
                gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Heal))
            SetRecoveredActionClearingFocus(state, playerId, gangSlot, GangAction.Heal);
        else if (CountPreviousFamilyFourHidesInSector(
                     state, playerId, gang.SectorId) < 1)
            SetRecoveredActionClearingFocus(state, playerId, gangSlot, GangAction.Hide);
        else
            PrepareFamilyFourMove(
                state, playerId, gang, gangSlot, snapshot);
    }

    private static void PrepareFamilyFourAfterAttackSnitchOrMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible,
        int visibleWeight,
        GangAction previousAction,
        FamilyPlanningSnapshot snapshot)
    {
        if (visibleWeight == 10)
        {
            var draw = DrawHumanWeightedAttackTarget(
                state, playerId, gang, visible, visibleWeight);
            if (draw.Accepted)
                SetRecoveredFocusedAttack(state, playerId, gang, gangSlot, draw.Selected);
            else
                ClearFamilyFourActionAndAuxiliaries(state, playerId, gangSlot);
            return;
        }

        if (state.Sectors[gang.SectorId].Owner == playerId)
        {
            if (CountPreviousFamilyFourHidesInSector(
                    state, playerId, gang.SectorId) < 1)
                SetRecoveredActionClearingFocus(state, playerId, gangSlot, GangAction.Hide);
            else
                PrepareFamilyFourMove(
                    state, playerId, gang, gangSlot, snapshot);
            return;
        }

        if (OriginalAiFamilyFourRules.CanControlAfterRepeatedMove(
                previousAction,
                state.AiPlanning.OlderAction(playerId, gangSlot),
                CanSoloControl(state, playerId, gang)))
            SetRecoveredActionClearingFocus(state, playerId, gangSlot, GangAction.Control);
        else
            PrepareFamilyFourMove(
                state, playerId, gang, gangSlot, snapshot);
    }

    private static void PrepareFamilyFourAfterHideOrEquip(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible,
        int visibleWeight,
        FamilyPlanningSnapshot snapshot)
    {
        if (visibleWeight == 10)
        {
            RecoveredAttackDraw draw = default;
            for (var attempt = 0;
                 attempt < OriginalAiFamilyFourRules.AttackAttemptsAfterHideOrEquip;
                 attempt++)
            {
                draw = DrawHumanWeightedAttackTarget(
                    state, playerId, gang, visible, visibleWeight);
                if (draw.Accepted) break;
            }
            SetRecoveredFocusedAttack(state, playerId, gang, gangSlot, draw.Selected);
        }

        var player = state.FindPlayer(playerId)!;
        if (state.AiPlanning.PlannedAction(playerId, gangSlot) != GangAction.Attack
            && OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                state, player, gang, gangSlot) is { } upgrade)
            SetRecoveredFocusedReplacementEquipmentAction(
                state, playerId, gangSlot, upgrade);

        if (state.AiPlanning.PlannedAction(playerId, gangSlot)
            is GangAction.Attack or GangAction.Equip)
            return;

        if (state.Sectors[gang.SectorId].Owner == playerId
            && CountPreviousFamilyFourHidesInSector(
                state, playerId, gang.SectorId)
                <= OriginalAiFamilyFourRules.MaximumPreviousHidesInOwnedSector)
            SetRecoveredActionClearingFocus(state, playerId, gangSlot, GangAction.Hide);
        else
            PrepareFamilyFourMove(
                state, playerId, gang, gangSlot, snapshot);
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
        FamilyPlanningSnapshot snapshot)
    {
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 2,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 4,
            snapshot.SectorOwners,
            snapshot.SectorDisabled,
            snapshot.SectorGangCounts,
            canSoloControl: sectorId => CanSoloControl(state, playerId, gang, sectorId),
            hasPriorChaos: _ => false,
            isHostileOwner: owner =>
                state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
            isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                .Setup.Controller == PlayerController.Human,
            snapshot.PlayerOrder,
            state.Random);
        SetRecoveredFocusedMoveAction(state, playerId, gangSlot, target);
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
