namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static bool PrepareFamilySixCommand(
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

        if (visibleWeight < 1)
            PrepareFamilySixMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
        else
            PrepareContestedFamilySixCommand(
                state, playerId, gang, gangSlot, visible, visibleWeight,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);

        var turnsRemaining = Math.Max(0,
            ScenarioCatalog.Turns(state.Setup.Duration) - (state.Coordinator.Turn - 1));
        if (OriginalAiFamilySixRules.ShouldTerminateForGreed(
                state.Setup.Scenario, turnsRemaining))
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Terminate);
        return true;
    }

    private static void PrepareContestedFamilySixCommand(
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
            SetFamilySixEquipment(state, playerId, gangSlot, upgrade);
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
                    state, playerId, gang, gangSlot,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
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

    private static void SetFamilySixEquipment(
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

    private static void PrepareFamilySixMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        var strategicTarget = FirstUncoveredFamilySixTarget(state, playerId);
        var mode = strategicTarget is { } targetSector
            ? 0x40 + targetSector
            : 2;
        if (strategicTarget is { } target)
            state.AiPlanning.SetCoverageSector(playerId, gangSlot, target);
        var destination = OriginalAiSectorSelectionRules.Select(
            mode, gang.SectorId, playerId, 6,
            sectorOwners, sectorDisabled, sectorGangCounts,
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
            new AiActionTarget(checked((byte)destination), 0));
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
        state.AiPlanning.SetCoverageSector(playerId, gangSlot, destination);
    }

    private static int? FirstUncoveredFamilySixTarget(
        MatchState state,
        PlayerId playerId)
    {
        var player = state.FindPlayer(playerId)!;
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
        }
        return null;
    }
}
