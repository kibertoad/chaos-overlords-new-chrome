namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static void PrepareFamilySixCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var visible = VisibleOpponentsInSector(state, playerId, gang.SectorId);
        var visibleWeight = FirstVisibleOpponentWeight(state, playerId, visible);

        if (visibleWeight < 1)
            PrepareFamilySixMove(
                state, playerId, gang, gangSlot, snapshot);
        else
            PrepareContestedFamilySixCommand(
                state, playerId, gang, gangSlot, visible, visibleWeight, snapshot);

        TerminateForGreed(state, playerId, gangSlot);
    }

    private static void PrepareContestedFamilySixCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible,
        int visibleWeight,
        FamilyPlanningSnapshot snapshot)
    {
        var targetPool = SelectHumanWeightedTargetPool(
            state, playerId, gang.SectorId, visible, visibleWeight);
        var draw = DrawRecoveredAttackTarget(
            state, gang, visible, targetPool,
            OriginalAiFamilySixRules.CanAttackSelectedTarget);
        if (draw.Accepted)
        {
            SetRecoveredFocusedAttack(state, playerId, gang, gangSlot, draw.Selected);
            return;
        }

        var player = state.FindPlayer(playerId)!;
        if (OriginalAiEquipmentRules.SelectFamilySixUpgrade(
                state, player, gang, gangSlot) is { } upgrade)
        {
            SetRecoveredFocusedReplacementEquipmentAction(
                state, playerId, gangSlot, upgrade);
            return;
        }

        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        if (OriginalAiFamilySixRules.ShouldHeal(
                gang.Force, effectiveHeal,
                state.AiPlanning.PreviousAction(playerId, gangSlot), visibleWeight))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            state.AiPlanning.SetFocusValue(
                playerId, gangSlot, AiPlanningState.InactiveFocusValue);
            return;
        }

        // These branches are literal to the recovered handler even though the
        // cached visible weight makes them unreachable in an unchanged state.
        if (visibleWeight < 1)
        {
            if (CanSoloControl(state, playerId, gang))
            {
                state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Control);
                state.AiPlanning.SetFocusValue(
                    playerId, gangSlot, AiPlanningState.InactiveFocusValue);
            }
            else
                PrepareFamilySixMove(
                    state, playerId, gang, gangSlot, snapshot);
            return;
        }

        for (var attempt = 0;
             attempt < OriginalAiFamilySixRules.RetryAttackAttempts;
             attempt++)
        {
            draw = DrawRecoveredAttackTarget(
                state, gang, visible, targetPool,
                OriginalAiFamilySixRules.CanAttackSelectedTarget);
            if (draw.Accepted) break;
        }
        SetRecoveredFocusedAttack(state, playerId, gang, gangSlot, draw.Selected);
    }

    private static void PrepareFamilySixMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        // RULE-AI-025, FND-AI-059: with every weight-10 sector covered the guard target is the
        // end marker 100, and mode 0x40 + 100 scores no sector, so the tie draw covers the city.
        // The marker stored as the coverage sector is overwritten by the step below before any
        // other gang plans, so it is not kept.
        var strategicTarget = FirstUncoveredFamilySixTarget(state, playerId);
        var mode = strategicTarget is { } targetSector
            ? 0x40 + targetSector
            : 2;
        if (strategicTarget is { } target and < MatchLimits.SectorCount)
            state.AiPlanning.SetCoverageSector(playerId, gangSlot, target);
        var destination = OriginalAiSectorSelectionRules.Select(
            mode, gang.SectorId, playerId, 6,
            snapshot.SectorOwners, snapshot.SectorDisabled, snapshot.SectorGangCounts,
            canSoloControl: _ => true,
            hasPriorChaos: _ => false,
            isHostileOwner: owner =>
                state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
            isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                .Setup.Controller == PlayerController.Human,
            snapshot.PlayerOrder,
            state.Random);
        SetRecoveredFocusedMoveAction(state, playerId, gangSlot, destination);
        state.AiPlanning.SetCoverageSector(playerId, gangSlot, destination);
    }

    private static int? FirstUncoveredFamilySixTarget(
        MatchState state,
        PlayerId playerId)
    {
        var player = state.FindPlayer(playerId)!;
        var anyGuardTarget = false;
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
        {
            var hasVisibleHostileHuman = state.Players.Any(candidate =>
                candidate.Id != playerId
                && candidate.Status == PlayerStatus.Active
                && candidate.Setup.Controller == PlayerController.Human
                && state.AiStrategy.IsHostile(playerId, candidate.Id)
                && candidate.Gangs.Any(candidateGang => candidateGang.IsActive
                    && candidateGang.SectorId == sectorId
                    && state.CanPlayerDetectGang(playerId, candidateGang.Id)));
            if (!hasVisibleHostileHuman) continue;

            var covered = player.Gangs.Select((candidate, slot) => (candidate, slot))
                .Any(entry => entry.candidate.IsActive
                    && state.AiPlanning.Family(playerId, entry.slot) == 6
                    && (state.AiPlanning.FocusValue(playerId, entry.slot)
                            != AiPlanningState.InactiveFocusValue
                        ? entry.candidate.SectorId
                        : state.AiPlanning.CoverageSector(playerId, entry.slot)) == sectorId);
            if (!covered) return sectorId;
            anyGuardTarget = true;
        }
        return anyGuardTarget ? OriginalAiSectorSelectionRules.GuardTargetEndMarker : null;
    }
}
