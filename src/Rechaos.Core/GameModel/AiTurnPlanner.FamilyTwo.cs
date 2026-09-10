namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private static bool PrepareFamilyTwoCommand(
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
        var sectorWeight = visible.Count == 0
            ? 0
            : VisibleOpponentWeight(state, playerId, visible[0].Gang.Owner);

        if (OriginalAiEquipmentRules.SelectFamilyTwoUpgrade(
                state, player, gang, gangSlot) is { } upgrade)
            SetFamilyTwoEquipment(state, playerId, gangSlot, upgrade);
        else if (OriginalAiFamilyTwoRules.ShouldHeal(
                     gang.Force,
                     EffectiveStatisticsCalculator.ForGang(state, gang).Heal,
                     sectorWeight))
            SetFamilyTwoAction(state, playerId, gangSlot, GangAction.Heal);
        else if (state.Sectors[gang.SectorId].Owner == playerId)
            PrepareFamilyTwoMove(
                state, playerId, gang, gangSlot,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
        else
            PrepareFamilyTwoNonOwnedSector(
                state, playerId, gang, gangSlot, visible, sectorWeight,
                sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);

        ApplyFamilyTwoControlOverride(state, playerId, gang, gangSlot, visible);
        var turnsRemaining = Math.Max(0,
            ScenarioCatalog.Turns(state.Setup.Duration) - (state.Coordinator.Turn - 1));
        if (OriginalAiFamilyTwoRules.ShouldTerminateForGreed(
                state.Setup.Scenario, turnsRemaining))
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Terminate);
        return true;
    }

    private static void PrepareFamilyTwoNonOwnedSector(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible,
        int sectorWeight,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        var hostile = visible.Where(target =>
            state.AiStrategy.IsHostile(playerId, target.Gang.Owner)).ToArray();
        if (sectorWeight > 0 && hostile.Length > 0)
        {
            PrepareFamilyTwoAttack(state, playerId, gang, gangSlot, visible, hostile, sectorWeight);
            return;
        }

        var action = OriginalAiFamilyTwoRules.SelectLocalAction(
            state.AiPlanning.PreviousAction(playerId, gangSlot),
            state.Setup.Scenario,
            CanSoloControl(state, playerId, gang));
        if (action == GangAction.Control)
        {
            SetFamilyTwoAction(state, playerId, gangSlot, action);
            return;
        }
        PrepareFamilyTwoMove(
            state, playerId, gang, gangSlot,
            sectorOwners, sectorDisabled, sectorGangCounts, playerOrder);
    }

    private static void PrepareFamilyTwoAttack(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible,
        IReadOnlyList<ObjectiveTarget> hostile,
        int sectorWeight)
    {
        var targetPool = sectorWeight == 10
            ? visible.Where(target => state.FindPlayer(target.Gang.Owner)?
                    .Setup.Controller == PlayerController.Human).ToArray()
            : hostile;
        ObjectiveTarget selected = default;
        for (var attempt = 0; attempt < OriginalAiFamilyTwoRules.AttackAttempts; attempt++)
        {
            var ordinal = state.Random.NextInclusive(targetPool.Count);
            selected = targetPool[ordinal - 1];
            var comparisonTarget = visible[ordinal - 1].Gang;
            var attackerStats = EffectiveStatisticsCalculator.ForGang(state, gang);
            var targetStats = EffectiveStatisticsCalculator.ForGang(state, comparisonTarget);
            if (OriginalAiFamilyTwoRules.CanAttackSelectedTarget(
                    gang.Force, attackerStats.Combat, attackerStats.Defense,
                    comparisonTarget.Force, targetStats.Combat, targetStats.Defense))
                break;
        }
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Attack,
            new AiActionTarget(
                checked((byte)selected.Gang.Owner.Value),
                checked((byte)selected.Slot)));
        state.AiPlanning.SetFocusValue(playerId, gangSlot, gang.SectorId);
    }

    private static void ApplyFamilyTwoControlOverride(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<ObjectiveTarget> visible)
    {
        if (state.Sectors[gang.SectorId].Owner is not { } owner) return;
        var ownerState = state.FindPlayer(owner);
        if (ownerState is null) return;
        var visibleHumanCount = visible.Count(target =>
            state.FindPlayer(target.Gang.Owner)?.Setup.Controller == PlayerController.Human);
        var visibleOwnerCount = visible.Count(target => target.Gang.Owner == owner);
        if (!OriginalAiFamilyTwoRules.ShouldOverrideWithControl(
                state.AiStrategy.IsHostile(playerId, owner),
                ownerState.Setup.Controller == PlayerController.Human,
                visibleHumanCount,
                visibleOwnerCount,
                state.AiPlanning.PreviousAction(playerId, gangSlot),
                state.AiStrategy.HasSectorCombatAdvantageHostility(state, playerId, owner)))
            return;
        SetFamilyTwoAction(state, playerId, gangSlot, GangAction.Control);
    }

    private static void SetFamilyTwoEquipment(
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
        state.AiPlanning.SetFocusValue(playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }

    private static void PrepareFamilyTwoMove(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        IReadOnlyList<int> sectorOwners,
        IReadOnlyList<bool> sectorDisabled,
        IReadOnlyList<int> sectorGangCounts,
        IReadOnlyList<int> playerOrder)
    {
        var target = OriginalAiSectorSelectionRules.Select(
            mode: 6,
            sourceSectorId: gang.SectorId,
            player: playerId,
            family: 2,
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
            state.Random,
            hasHumanPlayers: state.Setup.Players.Any(candidate =>
                candidate.Controller == PlayerController.Human),
            scenarioStandings: OriginalAiScenarioStandingRules.Build(state));
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Move,
            new AiActionTarget(checked((byte)target), 0));
        state.AiPlanning.SetFocusValue(playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }

    private static void SetFamilyTwoAction(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        GangAction action)
    {
        state.AiPlanning.SetPlannedAction(playerId, gangSlot, action);
        state.AiPlanning.SetFocusValue(playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }
}
