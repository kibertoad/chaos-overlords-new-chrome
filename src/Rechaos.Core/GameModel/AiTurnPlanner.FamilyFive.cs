namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static void PrepareFamilyFiveCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var player = state.FindPlayer(playerId)!;
        var previousAction = state.AiPlanning.PreviousAction(playerId, gangSlot);
        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;

        if (OriginalAiFamilyFiveRules.UsesSupportSiteContinuation(previousAction))
            PrepareFamilyFiveSupportContinuation(
                state, playerId, gang, gangSlot, effectiveHeal, snapshot);
        else if (OriginalAiFamilyFiveRules.UsesOpponentContinuation(previousAction))
            PrepareFamilyFiveOpponentContinuation(
                state, playerId, gang, gangSlot, snapshot);
        else if (previousAction == GangAction.Influence)
            PrepareFamilyFiveInfluenceContinuation(
                state, player, gang, gangSlot, effectiveHeal, snapshot);
        else if (previousAction == GangAction.Snitch)
            PrepareFamilyFiveMove(
                state, playerId, gang, gangSlot, snapshot);

        ApplyFamilyFiveTerminalOverrides(state, playerId, gangSlot);
    }

    private static void PrepareFamilyFiveSupportContinuation(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        int effectiveHeal,
        FamilyPlanningSnapshot snapshot)
    {
        if (OriginalAiFamilyFiveRules.ShouldHeal(gang.Force, effectiveHeal))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            return;
        }

        PrepareFamilyFiveSupportSiteOrTerritorial(
            state, playerId, gang, gangSlot, snapshot);
    }

    private static void PrepareFamilyFiveSupportSiteOrTerritorial(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        if (state.Sectors[gang.SectorId].Owner == playerId
            && OriginalAiFamilyFiveRules.SelectHighestSupportUnfinishedSite(
                state, gang.SectorId) is { } siteSlot)
        {
            SetFamilyFiveInfluence(state, playerId, gangSlot, siteSlot);
            return;
        }

        if (CanSoloControl(state, playerId, gang))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Control);
            return;
        }

        PrepareFamilyFiveMove(
            state, playerId, gang, gangSlot, snapshot);
    }

    private static void PrepareFamilyFiveOpponentContinuation(
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
            PrepareFamilyFiveSupportSiteOrTerritorial(
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
            OriginalAiFamilyFiveRules.CanAttackSelectedTarget);
        if (!draw.Accepted)
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.None);
            return;
        }

        SetRecoveredAttackAction(state, playerId, gangSlot, draw.Selected);
    }

    private static void PrepareFamilyFiveInfluenceContinuation(
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

        if (OriginalAiFamilyFiveRules.ShouldHeal(gang.Force, effectiveHeal))
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
                SetFamilyFiveInfluence(
                    state, playerId, gangSlot, previousSiteSlot);
                return;
            }

            if (OriginalAiFamilyFiveRules.SelectHighestSupportUnfinishedSite(
                    state, gang.SectorId) is { } siteSlot)
            {
                SetFamilyFiveInfluence(state, playerId, gangSlot, siteSlot);
                return;
            }
        }

        PrepareFamilyFiveMove(
            state, playerId, gang, gangSlot, snapshot);
    }

    private static void ApplyFamilyFiveTerminalOverrides(
        MatchState state,
        PlayerId playerId,
        int gangSlot)
    {
        if (OriginalAiFamilyFiveRules.ThreeMoveTransitionFamily(
                state.Setup.Scenario,
                state.AiPlanning.PlannedAction(playerId, gangSlot),
                state.AiPlanning.PreviousAction(playerId, gangSlot),
                state.AiPlanning.OlderAction(playerId, gangSlot)) is { } family)
            state.AiPlanning.SetFamily(playerId, gangSlot, family);

        TerminateForGreed(state, playerId, gangSlot);
    }

    private static void SetFamilyFiveInfluence(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        int siteSlot) =>
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Influence,
            new AiActionTarget(checked((byte)siteSlot), 0));

    private static void PrepareFamilyFiveMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var player = state.FindPlayer(playerId)!;
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 7,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 5,
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
                OriginalAiFamilyFiveRules.UnfinishedSupportScore(state, sectorId),
            hasPriorInfluence: sectorId => player.Gangs
                .Select((candidate, slot) => (candidate, slot))
                .Any(entry => entry.candidate.IsActive
                    && entry.candidate.SectorId == sectorId
                    && state.AiPlanning.PreviousAction(playerId, entry.slot)
                        == GangAction.Influence));
        SetRecoveredMoveAction(state, playerId, gangSlot, target);
    }
}
