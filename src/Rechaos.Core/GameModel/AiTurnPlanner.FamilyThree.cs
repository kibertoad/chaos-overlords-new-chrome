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
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
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
            SetFamilyThreeInfluence(state, playerId, gangSlot, siteSlot);
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
        var visibleWeight = FirstVisibleOpponentWeight(state, playerId, visible);
        if (visibleWeight != 10)
        {
            PrepareFamilyThreeCashSiteOrTerritorial(
                state, playerId, gang, gangSlot, snapshot);
            return;
        }

        var owner = state.Sectors[gang.SectorId].Owner;
        var targetPool = owner is { } sectorOwner
            && state.AiStrategy.IsHostile(playerId, sectorOwner)
                ? visible.Where(candidate => state.FindPlayer(candidate.Gang.Owner)?
                        .Setup.Controller == PlayerController.Human)
                    .ToArray()
                : visible;
        var draw = DrawRecoveredAttackTarget(
            state, gang, visible, targetPool,
            OriginalAiFamilyThreeRules.CanAttackSelectedTarget);
        if (!draw.Accepted)
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.None);
            return;
        }

        SetRecoveredAttackAction(state, playerId, gangSlot, draw.Selected);
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
            SetRecoveredReplacementEquipmentAction(
                state, playerId, gangSlot, upgrade.ItemId, upgrade.Slot);
            return;
        }

        if (OriginalAiFamilyThreeRules.ShouldHeal(gang.Force, effectiveHeal))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
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
                    state, playerId, gangSlot, previousSiteSlot);
                return;
            }

            if (OriginalAiFamilyThreeRules.SelectHighestCashUnfinishedSite(
                    state, gang.SectorId) is { } siteSlot)
            {
                SetFamilyThreeInfluence(state, playerId, gangSlot, siteSlot);
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

    private static void SetFamilyThreeInfluence(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        int siteSlot) =>
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Influence,
            new AiActionTarget(checked((byte)siteSlot), 0));

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
            isHostileOwner: owner =>
                state.AiStrategy.IsHostile(playerId, new PlayerId(owner)),
            isHumanOwner: owner => state.FindPlayer(new PlayerId(owner))?
                .Setup.Controller == PlayerController.Human,
            snapshot.PlayerOrder,
            state.Random,
            unfinishedSiteScore: sectorId =>
                OriginalAiFamilyThreeRules.UnfinishedCashScore(state, sectorId));
        SetRecoveredMoveAction(state, playerId, gangSlot, target);
    }
}
