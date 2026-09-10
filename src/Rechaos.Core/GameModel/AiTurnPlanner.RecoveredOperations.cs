namespace Rechaos.Core.GameModel;

public static partial class AiTurnPlanner
{
    private readonly record struct RecoveredAttackDraw(
        ObjectiveTarget Selected,
        bool Accepted);

    private static IReadOnlyList<ObjectiveTarget> SelectHumanWeightedTargetPool(
        MatchState state,
        PlayerId playerId,
        int sectorId,
        IReadOnlyList<ObjectiveTarget> visible,
        int visibleWeight)
    {
        var owner = state.Sectors[sectorId].Owner;
        return owner is { } sectorOwner
            && state.AiStrategy.IsHostile(playerId, sectorOwner)
            && visibleWeight == 10
                ? visible.Where(candidate => state.FindPlayer(candidate.Gang.Owner)?
                        .Setup.Controller == PlayerController.Human)
                    .ToArray()
                : visible;
    }

    private static RecoveredAttackDraw DrawRecoveredAttackTarget(
        MatchState state,
        MatchGangState gang,
        IReadOnlyList<ObjectiveTarget> visible,
        IReadOnlyList<ObjectiveTarget> targetPool,
        Func<int, int, int, int, int, int, bool> acceptsComparison)
    {
        var ordinal = state.Random.NextInclusive(targetPool.Count);
        var selected = targetPool[ordinal - 1];
        var comparisonTarget = visible[ordinal - 1].Gang;
        var attackerStats = EffectiveStatisticsCalculator.ForGang(state, gang);
        var targetStats = EffectiveStatisticsCalculator.ForGang(state, comparisonTarget);
        return new RecoveredAttackDraw(selected, acceptsComparison(
            gang.Force, attackerStats.Combat, attackerStats.Defense,
            comparisonTarget.Force, targetStats.Combat, targetStats.Defense));
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
}
