namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static void PrepareFamilyThreeCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var player = state.FindPlayer(playerId)!;
        var previousAction = state.AiPlanning.PreviousAction(playerId, gangSlot);
        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;

        if (OriginalAiFamilyThreeRules.UsesCashSiteContinuation(previousAction))
            PrepareFamilyThreeCashContinuation(
                state, playerId, gang, gangSlot, effectiveHeal, snapshot);
        else if (OriginalAiFamilyThreeRules.UsesOpponentContinuation(previousAction))
            PrepareFamilyThreeOpponentContinuation(
                state, playerId, gang, gangSlot, snapshot);
        else if (previousAction == GangAction.Influence)
            PrepareFamilyThreeInfluenceContinuation(
                state, player, gang, gangSlot, effectiveHeal, snapshot);
        else if (previousAction == GangAction.Snitch)
            PrepareFamilyThreeMove(
                state, playerId, gang, gangSlot, snapshot);

        ApplyFamilyThreeTerminalOverrides(state, playerId, gangSlot);
    }

    private static void PrepareFamilyThreeCashContinuation(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        int effectiveHeal,
        FamilyPlanningSnapshot snapshot)
    {
        if (OriginalAiFamilyThreeRules.ShouldHeal(gang.Force, effectiveHeal))
        {
            SetRecoveredActionClearingFocus(state, playerId, gangSlot, GangAction.Heal);
            return;
        }

        PrepareFamilyThreeCashSiteOrTerritorial(
            state, playerId, gang, gangSlot, snapshot);
    }

    private static void PrepareFamilyThreeCashSiteOrTerritorial(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        if (state.Sectors[gang.SectorId].Owner == playerId
            && OriginalAiFamilyThreeRules.SelectHighestCashUnfinishedSite(
                state, gang.SectorId) is { } siteSlot)
        {
            SetFamilyThreeInfluence(state, playerId, gang, gangSlot, siteSlot);
            return;
        }

        if (CanSoloControl(state, playerId, gang))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Control);
            return;
        }

        PrepareFamilyThreeMove(
            state, playerId, gang, gangSlot, snapshot);
    }

    private static void PrepareFamilyThreeOpponentContinuation(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var visible = VisibleOpponentsInSector(state, playerId, gang.SectorId);
        var visibleWeight = state.AiPlanning.SectorWeight(playerId, gang.SectorId);
        if (visibleWeight != 10)
        {
            PrepareFamilyThreeCashSiteOrTerritorial(
                state, playerId, gang, gangSlot, snapshot);
            return;
        }

        var targetPool = IsHostileOwner(state, playerId, gang.SectorId)
                ? visible.Where(candidate => state.FindPlayer(candidate.Gang.Owner)?
                        .Setup.Controller == PlayerController.Human)
                    .ToArray()
                : visible;
        // BUG-AI-007, call 0x00436650: the strength test is handed the sector as a slot.
        var draw = DrawRecoveredAttackTarget(
            state, gang, visible, targetPool,
            OriginalAiFamilyThreeRules.CanAttackSelectedTarget, strengthTestSlot: gang.SectorId);
        if (!draw.Accepted)
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.None);
            state.AiPlanning.SetFocusValue(playerId, gangSlot, AiPlanningState.InactiveFocusValue);
            state.AiPlanning.SetCoverageSector(playerId, gangSlot, AiPlanningState.InactiveCoverageSector);
            return;
        }

        SetRecoveredFocusedAttack(state, playerId, gang, gangSlot, draw.Selected);
    }

    private static void PrepareFamilyThreeInfluenceContinuation(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        int effectiveHeal,
        FamilyPlanningSnapshot snapshot)
    {
        var playerId = player.Id;
        if (OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                state, player, gang, gangSlot) is { } upgrade)
        {
            SetRecoveredFocusedReplacementEquipmentAction(
                state, playerId, gangSlot, upgrade);
            return;
        }

        if (OriginalAiFamilyThreeRules.ShouldHeal(gang.Force, effectiveHeal))
        {
            SetRecoveredActionClearingFocus(state, playerId, gangSlot, GangAction.Heal);
            return;
        }

        if (state.Sectors[gang.SectorId].Owner == playerId)
        {
            var previousSiteSlot = state.AiPlanning
                .PreviousTarget(playerId, gangSlot).First;
            if (previousSiteSlot < MatchLimits.SitesPerSector
                && state.Sectors[gang.SectorId].Sites[previousSiteSlot].Resistance > 0)
            {
                SetFamilyThreeInfluence(
                    state, playerId, gang, gangSlot, previousSiteSlot);
                return;
            }

            if (OriginalAiFamilyThreeRules.SelectHighestCashUnfinishedSite(
                    state, gang.SectorId) is { } siteSlot)
            {
                SetFamilyThreeInfluence(state, playerId, gang, gangSlot, siteSlot);
                return;
            }
        }

        PrepareFamilyThreeMove(
            state, playerId, gang, gangSlot, snapshot);
    }

    private static void ApplyFamilyThreeTerminalOverrides(
        MatchState state,
        PlayerId playerId,
        int gangSlot)
    {
        if (OriginalAiFamilyThreeRules.ThreeMoveTransitionFamily(
                state.Setup.Scenario,
                state.AiPlanning.PlannedAction(playerId, gangSlot),
                state.AiPlanning.PreviousAction(playerId, gangSlot),
                state.AiPlanning.OlderAction(playerId, gangSlot)) is { } family)
            state.AiPlanning.SetFamily(playerId, gangSlot, family);

        TerminateForGreed(state, playerId, gangSlot);
    }

    // The handler stores the gang's sector as its focus with every Influence it plans.
    private static void SetFamilyThreeInfluence(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        int siteSlot)
    {
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Influence,
            new AiActionTarget(checked((byte)siteSlot), 0));
        state.AiPlanning.SetFocusValue(playerId, gangSlot, gang.SectorId);
    }

    private static void PrepareFamilyThreeMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 8,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 3,
            snapshot.SectorOwners,
            snapshot.SectorDisabled,
            snapshot.SectorGangCounts,
            canSoloControl: _ => true,
            hasPriorChaos: _ => false,
            ownerTests: SelectorOwnerTests(state, playerId),
            state.Random,
            unfinishedSiteScore: sectorId =>
                OriginalAiFamilyThreeRules.UnfinishedCashScore(state, sectorId), planning: state.AiPlanning);
        SetRecoveredFocusedMoveAction(state, playerId, gangSlot, target);
    }
}
