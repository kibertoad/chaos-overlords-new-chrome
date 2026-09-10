namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static bool PrepareFamilySevenCommand(
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
        var visible = VisibleOpponentsInSector(state, playerId, gang.SectorId);
        var visibleWeight = visible.Count == 0
            ? 0
            : VisibleOpponentWeight(state, playerId, visible[0].Gang.Owner);

        if (visibleWeight == 10)
            TryPrepareFamilySevenAttack(state, playerId, gang, gangSlot, visible);
        else if (OriginalAiEquipmentRules.SelectFamilyOneUpgrade(
                     state, player, gang, gangSlot) is { } upgrade)
            SetFamilySevenEquipment(state, playerId, gangSlot, upgrade);

        var plannedAction = state.AiPlanning.PlannedAction(playerId, gangSlot);
        if (!OriginalAiFamilySevenRules.EndsAfterPreliminaryAction(plannedAction))
            PrepareFamilySevenResearchContinuation(
                state, player, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);

        var turnsRemaining = Math.Max(0,
            ScenarioCatalog.Turns(state.Setup.Duration) - (state.Coordinator.Turn - 1));
        if (OriginalAiFamilySevenRules.ShouldTerminateForGreed(
                state.Setup.Scenario, turnsRemaining))
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Terminate);
        return true;
    }

    private static void SetFamilySevenEquipment(
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

    private static void TryPrepareFamilySevenAttack(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible)
    {
        var owner = state.Sectors[gang.SectorId].Owner;
        var targetPool = owner is { } sectorOwner
            && state.AiStrategy.IsHostile(playerId, sectorOwner)
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
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
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
                state, player, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
            return;
        }

        if (gang.SectorId != bestSector)
        {
            var target = OriginalAiSectorSelectionRules.Select(
                mode: 0x40 + bestSector,
                sourceSectorId: gang.SectorId,
                player: playerId,
                family: 7,
                sectorOwners,
                sectorDisabled,
                sectorGangCounts,
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
                new AiActionTarget(checked((byte)target), 0));
            state.AiPlanning.SetFocusValue(
                playerId, gangSlot, AiPlanningState.InactiveFocusValue);
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
            state, player, gang, gangSlot,
            sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
    }

    private static void PrepareFamilySevenResearch(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
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
            playerOrderValues: playerOrder,
            random: state.Random);
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Move,
            new AiActionTarget(checked((byte)target), 0));
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }
}
