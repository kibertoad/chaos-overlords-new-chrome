namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    /// <summary>
    /// The two places where families 3 and 5 differ. Both run the site-influence planner below:
    /// family 3 picks the unfinished site with the highest Cash and moves with selector mode 8,
    /// family 5 picks by Support and moves with mode 7. Every other rule the planner consults has
    /// the same body in <see cref="OriginalAiFamilyThreeRules"/> and
    /// <see cref="OriginalAiFamilyFiveRules"/>, so the planner reads the family 3 copy.
    /// </summary>
    private sealed record SiteInfluenceFamily(
        Func<MatchState, int, int?> SelectUnfinishedSite,
        Action<MatchState, PlayerId, MatchGangState, int, FamilyPlanningSnapshot> Move);

    private static readonly SiteInfluenceFamily FamilyThreeSiteInfluence = new(
        OriginalAiFamilyThreeRules.SelectHighestCashUnfinishedSite, PrepareFamilyThreeMove);

    private static readonly SiteInfluenceFamily FamilyFiveSiteInfluence = new(
        OriginalAiFamilyFiveRules.SelectHighestSupportUnfinishedSite, PrepareFamilyFiveMove);

    private static void PrepareFamilyThreeCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot) =>
        PrepareSiteInfluenceFamilyCommand(
            state, playerId, gang, gangSlot, snapshot, FamilyThreeSiteInfluence);

    private static void PrepareSiteInfluenceFamilyCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot,
        SiteInfluenceFamily variant)
    {
        var player = state.FindPlayer(playerId)!;
        var previousAction = state.AiPlanning.PreviousAction(playerId, gangSlot);
        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;

        if (OriginalAiFamilyThreeRules.UsesCashSiteContinuation(previousAction))
            PrepareSiteInfluenceSiteContinuation(
                state, playerId, gang, gangSlot, effectiveHeal, snapshot, variant);
        else if (OriginalAiFamilyThreeRules.UsesOpponentContinuation(previousAction))
            PrepareSiteInfluenceOpponentContinuation(
                state, playerId, gang, gangSlot, snapshot, variant);
        else if (previousAction == GangAction.Influence)
            PrepareSiteInfluenceRepeatContinuation(
                state, player, gang, gangSlot, effectiveHeal, snapshot, variant);
        else if (previousAction == GangAction.Snitch)
            variant.Move(state, playerId, gang, gangSlot, snapshot);

        ApplySiteInfluenceTerminalOverrides(state, playerId, gangSlot);
    }

    private static void PrepareSiteInfluenceSiteContinuation(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        int effectiveHeal,
        FamilyPlanningSnapshot snapshot,
        SiteInfluenceFamily variant)
    {
        if (OriginalAiFamilyThreeRules.ShouldHeal(gang.Force, effectiveHeal))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            return;
        }

        PrepareSiteInfluenceSiteOrTerritorial(
            state, playerId, gang, gangSlot, snapshot, variant);
    }

    private static void PrepareSiteInfluenceSiteOrTerritorial(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot,
        SiteInfluenceFamily variant)
    {
        if (state.Sectors[gang.SectorId].Owner == playerId
            && variant.SelectUnfinishedSite(state, gang.SectorId) is { } siteSlot)
        {
            SetSiteInfluenceAction(state, playerId, gangSlot, siteSlot);
            return;
        }

        if (CanSoloControl(state, playerId, gang))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Control);
            return;
        }

        variant.Move(state, playerId, gang, gangSlot, snapshot);
    }

    private static void PrepareSiteInfluenceOpponentContinuation(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot,
        SiteInfluenceFamily variant)
    {
        var visible = VisibleOpponentsInSector(state, playerId, gang.SectorId);
        var visibleWeight = FirstVisibleOpponentWeight(state, playerId, visible);
        if (visibleWeight != 10)
        {
            PrepareSiteInfluenceSiteOrTerritorial(
                state, playerId, gang, gangSlot, snapshot, variant);
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

    private static void PrepareSiteInfluenceRepeatContinuation(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        int effectiveHeal,
        FamilyPlanningSnapshot snapshot,
        SiteInfluenceFamily variant)
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
                SetSiteInfluenceAction(
                    state, playerId, gangSlot, previousSiteSlot);
                return;
            }

            if (variant.SelectUnfinishedSite(state, gang.SectorId) is { } siteSlot)
            {
                SetSiteInfluenceAction(state, playerId, gangSlot, siteSlot);
                return;
            }
        }

        variant.Move(state, playerId, gang, gangSlot, snapshot);
    }

    private static void ApplySiteInfluenceTerminalOverrides(
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

    private static void SetSiteInfluenceAction(
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
