namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private readonly record struct RecoveredAttackDraw(
        ObjectiveTarget Selected,
        bool Accepted);

    /// <summary>RULE-AI-004 owner_query: -2 under police presence, else the owner byte.</summary>
    internal static int OwnerQuery(MatchState state, int sectorId)
    {
        var sector = state.Sectors[sectorId];
        return sector.CrackdownActive ? -2 : sector.Owner?.Value ?? -1;
    }

    /// <summary>RULE-AI-004 hostile_owner, with the original's out-of-row reads (FND-AI-048).</summary>
    internal static bool IsHostileOwner(MatchState state, PlayerId playerId, int sectorId) =>
        state.AiStrategy.IsHostileToOwnerQuery(playerId, OwnerQuery(state, sectorId));

    private static IReadOnlyList<ObjectiveTarget> SelectHumanWeightedTargetPool(
        MatchState state,
        PlayerId playerId,
        int sectorId,
        IReadOnlyList<ObjectiveTarget> visible,
        int visibleWeight)
    {
        return IsHostileOwner(state, playerId, sectorId)
            && visibleWeight == 10
                ? visible.Where(candidate => state.FindPlayer(candidate.Gang.Owner)?
                        .Setup.Controller == PlayerController.Human)
                    .ToArray()
                : visible;
    }

    /// <summary>Weight of the first visible opponent's owner, or 0 when nobody is visible.</summary>
    private static int FirstVisibleOpponentWeight(
        MatchState state,
        PlayerId playerId,
        IReadOnlyList<ObjectiveTarget> visible) =>
        visible.Count == 0
            ? 0
            : VisibleOpponentWeight(state, playerId, visible[0].Gang.Owner);

    /// <summary>The family 0 and family 4 draw: human-weighted pool, family 12 acceptance test.</summary>
    private static RecoveredAttackDraw DrawHumanWeightedAttackTarget(
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
            OriginalAiFamilyTwelveRules.CanAttackSelectedTarget);
    }

    private static RecoveredAttackDraw DrawRecoveredAttackTarget(
        MatchState state,
        MatchGangState gang,
        IReadOnlyList<ObjectiveTarget> visible,
        IReadOnlyList<ObjectiveTarget> targetPool,
        Func<int, int, int, int, int, int, bool> acceptsComparison)
    {
        // Families 12 and 13/14 clamp their pool before they get here and the other callers only
        // reach this with a hostile list they have already found non-empty. Say so out loud, so a
        // future caller meets a named precondition rather than a divide by zero inside the RNG.
        if (targetPool.Count == 0)
            throw new InvalidOperationException("A recovered attack draw needs a non-empty target pool.");
        var ordinal = state.Random.NextInclusive(targetPool.Count);
        var selected = targetPool[ordinal - 1];
        var comparisonTarget = visible[ordinal - 1].Gang;
        var attackerStats = EffectiveStatisticsCalculator.ForGang(state, gang);
        var targetStats = EffectiveStatisticsCalculator.ForGang(state, comparisonTarget);
        return new RecoveredAttackDraw(selected, acceptsComparison(
            gang.Force, attackerStats.Combat, attackerStats.Defense,
            comparisonTarget.Force, targetStats.Combat, targetStats.Defense));
    }

    private static void SetRecoveredActionClearingFocus(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        GangAction action)
    {
        state.AiPlanning.SetPlannedAction(playerId, gangSlot, action);
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }

    /// <summary>Replaces the planned action with Terminate in the last turns of a Greed match.</summary>
    private static void TerminateForGreed(MatchState state, PlayerId playerId, int gangSlot)
    {
        var turnsRemaining = Math.Max(0,
            ScenarioCatalog.Turns(state.Setup.Duration) - (state.Coordinator.Turn - 1));
        if (OriginalAiFamilyTwelveRules.ShouldTerminateForGreed(
                state.Setup.Scenario, turnsRemaining))
            state.AiPlanning.SetPlannedAction(playerId, gangSlot, GangAction.Terminate);
    }

    private static void SetRecoveredAttackAction(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        ObjectiveTarget selected)
    {
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Attack,
            new AiActionTarget(
                checked((byte)selected.Gang.Owner.Value),
                checked((byte)selected.Slot)));
    }

    private static void SetRecoveredFocusedAttack(
        MatchState state,
        PlayerId playerId,
        MatchGangState gang,
        int gangSlot,
        ObjectiveTarget selected)
    {
        SetRecoveredAttackAction(state, playerId, gangSlot, selected);
        state.AiPlanning.SetFocusValue(playerId, gangSlot, gang.SectorId);
    }

    private static void SetRecoveredReplacementEquipmentAction(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        short itemId,
        EquipmentSlot slot)
    {
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Equip,
            new AiActionTarget(checked((byte)itemId), 0));
        state.AiPlanning.SetEquipmentCooldown(
            playerId, gangSlot, slot,
            OriginalAiEquipmentRules.EquipmentReplacementCooldown(
                state.Definitions.Items[itemId].Cost));
    }

    private static void SetRecoveredFocusedReplacementEquipmentAction(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        OriginalAiEquipmentRules.Upgrade upgrade)
    {
        SetRecoveredReplacementEquipmentAction(
            state, playerId, gangSlot, upgrade.ItemId, upgrade.Slot);
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }

    private static void SetRecoveredMoveAction(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        int destination)
    {
        state.AiPlanning.SetPlannedAction(
            playerId, gangSlot, GangAction.Move,
            new AiActionTarget(checked((byte)destination), 0));
    }

    private static void SetRecoveredFocusedMoveAction(
        MatchState state,
        PlayerId playerId,
        int gangSlot,
        int destination)
    {
        SetRecoveredMoveAction(state, playerId, gangSlot, destination);
        state.AiPlanning.SetFocusValue(
            playerId, gangSlot, AiPlanningState.InactiveFocusValue);
    }
}
