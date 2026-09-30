namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static void PrepareFamilySevenCommand(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var player = state.FindPlayer(playerId)!;
        var visible = VisibleOpponentsInSector(state, playerId, gang.SectorId);
        var visibleWeight = state.AiPlanning.SectorWeight(playerId, gang.SectorId);

        if (visibleWeight == 10)
            TryPrepareFamilySevenAttack(state, playerId, gang, gangSlot, visible);
        else if (OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                     state, player, gang, gangSlot) is { } upgrade)
            SetRecoveredFocusedReplacementEquipmentAction(
                state, playerId, gangSlot, upgrade);

        var plannedAction = state.AiPlanning.PlannedAction(playerId, gangSlot);
        if (!OriginalAiFamilySevenRules.EndsAfterPreliminaryAction(plannedAction))
            PrepareFamilySevenResearchContinuation(
                state, player, gang, gangSlot, snapshot);

        TerminateForGreed(state, playerId, gangSlot);
    }

    private static void TryPrepareFamilySevenAttack(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible)
    {
        var targetPool = IsHostileOwner(state, playerId, gang.SectorId)
                ? visible.Where(candidate => state.FindPlayer(candidate.Gang.Owner)?
                        .Setup.Controller == PlayerController.Human)
                    .ToArray()
                : visible;
        var draw = DrawRecoveredAttackTarget(
            state, gang, visible, targetPool,
            OriginalAiFamilySevenRules.CanAttackSelectedTarget);
        if (!draw.Accepted
            || !state.AiStrategy.IsHostile(playerId, draw.Selected.Gang.Owner))
            return;

        SetRecoveredFocusedAttack(state, playerId, gang, gangSlot, draw.Selected);
    }

    private static void PrepareFamilySevenResearchContinuation(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var playerId = player.Id;
        var previousAction = state.AiPlanning.PreviousAction(playerId, gangSlot);
        if (OriginalAiFamilySevenRules.ClearsPreviousTarget(previousAction))
            state.AiPlanning.ClearPreviousTargetFirst(playerId, gangSlot);

        var effectiveHeal = EffectiveStatisticsCalculator.ForGang(state, gang).Heal;
        if (OriginalAiFamilySevenRules.ShouldHeal(gang.Force, effectiveHeal))
        {
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Heal);
            return;
        }

        var bestSector = OriginalAiFamilySevenRules.SelectBestOwnedResearchSector(
            state, playerId, gang.SectorId);
        if (state.AiPlanning.FocusValue(playerId, gangSlot) == bestSector)
        {
            PrepareFamilySevenResearch(
                state, player, gang, gangSlot, snapshot);
            return;
        }

        if (gang.SectorId != bestSector)
        {
            var target = OriginalAiSectorSelectionRules.Select(
                mode: 0x40 + bestSector,
                sourceSectorId: gang.SectorId,
                player: playerId,
                family: 7,
                snapshot.SectorOwners,
                snapshot.SectorDisabled,
                snapshot.SectorGangCounts,
                canSoloControl: _ => true,
                hasPriorChaos: _ => false,
                ownerIsHuman: sectorId => OwnerIsHuman(state, sectorId),
                multipliesByFive: sectorId => MultipliesSelectorScore(state, playerId, sectorId),
                state.Random, planning: state.AiPlanning);
            SetRecoveredFocusedMoveAction(state, playerId, gangSlot, target);
            return;
        }

        if (OriginalAiFamilySevenRules.SelectFirstUnfinishedResearchSite(
                state, gang.SectorId) is { } siteSlot)
        {
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Influence,
                new AiActionTarget(checked((byte)siteSlot), 0));
            return;
        }

        state.AiPlanning.SetFocusValue(playerId, gangSlot, gang.SectorId);
        PrepareFamilySevenResearch(
            state, player, gang, gangSlot, snapshot);
    }

    private static void PrepareFamilySevenResearch(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        FamilyPlanningSnapshot snapshot)
    {
        var playerId = player.Id;
        var previousItem = state.AiPlanning.PreviousTarget(playerId, gangSlot).First;
        var itemId = OriginalAiFamilySevenRules.SelectContinuationResearchItem(
                state, player, gang, previousItem)
            ?? OriginalAiFamilySevenRules.SelectFallbackResearchItem(
                state, player, gang);
        if (itemId is { } selected)
        {
            state.AiPlanning.SetPlannedAction(
                playerId, gangSlot, GangAction.Research,
                new AiActionTarget(checked((byte)selected), 0));
            state.AiPlanning.SetFocusValue(playerId, gangSlot, selected);
            return;
        }

        state.AiPlanning.SetFamily(playerId, gangSlot, 0);
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 5,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 0,
            snapshot.SectorOwners,
            snapshot.SectorDisabled,
            snapshot.SectorGangCounts,
            canSoloControl: sectorId => CanSoloControl(state, playerId, gang, sectorId),
            hasPriorChaos: sectorId =>
                CountPreviousChaosInSector(state, playerId, sectorId) > 0,
            ownerIsHuman: sectorId => OwnerIsHuman(state, sectorId),
            multipliesByFive: sectorId => MultipliesSelectorScore(state, playerId, sectorId),
            random: state.Random, planning: state.AiPlanning);
        SetRecoveredFocusedMoveAction(state, playerId, gangSlot, target);
    }
}
