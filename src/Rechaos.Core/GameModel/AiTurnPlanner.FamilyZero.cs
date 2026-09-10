namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static bool PrepareFamilyZeroCommand(
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
                PrepareFamilyZeroAfterNone(
                    state, playerId, gang, gangSlot,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
                break;
            case GangAction.Attack:
                PrepareFamilyZeroAfterAttack(
                    state, playerId, gang, gangSlot, visible, visibleWeight,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
                break;
            case GangAction.Hide:
            case GangAction.Equip:
                PrepareFamilyZeroAfterHideOrEquip(
                    state, playerId, gang, gangSlot, visible, visibleWeight,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
                break;
            case GangAction.Control:
                if (state.Sectors[gang.SectorId].Owner == playerId)
                    SetFamilyZeroAction(state, playerId, gangSlot, GangAction.Hide);
                else
                    PrepareFamilyZeroMove(
                        state, playerId, gang, gangSlot,
                        sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
                break;
            case GangAction.Heal:
            case GangAction.Snitch:
            case GangAction.Move:
                PrepareFamilyZeroAfterHealSnitchOrMove(
                    state, playerId, gang, gangSlot, visible, visibleWeight,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
                break;
            case GangAction.Research:
                PrepareFamilyZeroMove(
                    state, playerId, gang, gangSlot,
                    sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
                break;
        }

        if (OriginalAiFamilyZeroRules.FamilyAfterPlanning(
                state.Setup.Scenario,
                state.AiPlanning.PlannedAction(playerId, gangSlot),
                state.AiPlanning.OlderAction(playerId, gangSlot)) is { } family)
            state.AiPlanning.SetFamily(playerId, gangSlot, family);
        return true;
    }

    private static void PrepareFamilyZeroAfterNone(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        if (OriginalAiFamilyZeroRules.ShouldHeal(
                gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Heal))
        {
            SetFamilyZeroAction(state, playerId, gangSlot, GangAction.Heal);
            return;
        }

        if (!HasPreviousFamilyZeroHideInSector(state, playerId, gang.SectorId))
            SetFamilyZeroAction(state, playerId, gangSlot, GangAction.Hide);
        else
            PrepareFamilyZeroMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
    }

    private static void PrepareFamilyZeroAfterAttack(
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
        if (visibleWeight != 10)
        {
            PrepareFamilyZeroMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
            return;
        }

        var draw = DrawFamilyZeroTarget(
            state, playerId, gang, visible, visibleWeight);
        if (draw.Accepted)
        {
            SetRecoveredFocusedAttack(state, playerId, gang, gangSlot, draw.Selected);
            return;
        }

        if (CanSoloControl(state, playerId, gang))
            // This branch uniquely leaves the first auxiliary short unchanged.
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Control);
        else
            PrepareFamilyZeroMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
    }

    private static void PrepareFamilyZeroAfterHideOrEquip(
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
                 attempt < OriginalAiFamilyZeroRules.AttackAttemptsAfterHideOrEquip;
                 attempt++)
            {
                draw = DrawFamilyZeroTarget(
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

        if (state.Sectors[gang.SectorId].Owner == playerId)
        {
            if (OriginalAiFamilyZeroRules.ShouldHeal(
                    gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Heal))
                SetFamilyZeroAction(state, playerId, gangSlot, GangAction.Heal);
            else
                SetFamilyZeroAction(state, playerId, gangSlot, GangAction.Hide);
            return;
        }

        // The handler repeats a weight-10 draw here, but reaching this point
        // with that cached weight would already have prepared Attack above.
        PrepareFamilyZeroMove(
            state, playerId, gang, gangSlot,
            sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
    }

    private static void PrepareFamilyZeroAfterHealSnitchOrMove(
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
        if (OriginalAiFamilyZeroRules.ShouldHeal(
                gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Heal))
        {
            // Unlike the other family-0 Heal sites, this write preserves the
            // auxiliary values.
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            return;
        }

        if (visibleWeight == 10)
        {
            var draw = DrawFamilyZeroTarget(
                state, playerId, gang, visible, visibleWeight);
            if (draw.Accepted)
                SetRecoveredFocusedAttack(state, playerId, gang, gangSlot, draw.Selected);
            else
            {
                state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.None);
                state.AiPlanning.SetFocusValue(
                    playerId, gangSlot, AiPlanningState.InactiveFocusValue);
                state.AiPlanning.SetCoverageSector(
                    playerId, gangSlot, AiPlanningState.InactiveCoverageSector);
            }
            return;
        }

        var owned = state.Sectors[gang.SectorId].Owner == playerId;
        if (!owned && CanSoloControl(state, playerId, gang))
        {
            SetFamilyZeroAction(state, playerId, gangSlot, GangAction.Control);
            return;
        }

        if (!HasPreviousFamilyZeroHideInSector(state, playerId, gang.SectorId))
            SetFamilyZeroAction(state, playerId, gangSlot, GangAction.Hide);
        else
            PrepareFamilyZeroMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
    }

    private static RecoveredAttackDraw DrawFamilyZeroTarget(
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
            OriginalAiFamilyZeroRules.CanAttackSelectedTarget);
    }

    private static void SetFamilyZeroAction(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        GangAction action)
    {
        state.AiPlanning.SetPlannedAction(playerId, gangSlot, action);
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }

    private static void PrepareFamilyZeroMove(
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
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 5,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 0,
            sectorOwners,
            sectorDisabled,
            sectorGangCounts,
            canSoloControl: sectorId => CanSoloControl(state, playerId, gang, sectorId),
            hasPriorChaos: sectorId => player.Gangs
                .Select((candidate, slot) => (candidate, slot))
                .Any(entry => entry.candidate.IsActive
                    && entry.candidate.SectorId == sectorId
                    && state.AiPlanning.PreviousAction(playerId, entry.slot)
                        == GangAction.Chaos),
            isHostileOwner: owner =>
                state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
            isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                .Setup.Controller == PlayerController.Human,
            playerOrder,
            state.Random);
        SetRecoveredFocusedMoveAction(state, playerId, gangSlot, target);
    }

    private static bool HasPreviousFamilyZeroHideInSector(
        MatchState state,
        PlayerId playerId,
        int sectorId)
    {
        var player = state.FindPlayer(playerId)!;
        return player.Gangs.Select((gang, slot) => (gang, slot))
            .Any(entry => entry.gang.IsActive
                && entry.gang.SectorId == sectorId
                && state.AiPlanning.PreviousAction(playerId, entry.slot)
                    == GangAction.Hide);
    }
}
