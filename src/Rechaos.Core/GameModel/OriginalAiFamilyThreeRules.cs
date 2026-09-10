namespace Rechaos.Core.GameModel;

/// <summary>
/// Cash-site Influence decisions recovered from original AI family 3 at
/// 0x00435bd0. Outer target enumeration remains in <see cref="AiTurnPlanner"/>.
/// </summary>
internal static class OriginalAiFamilyThreeRules
{
    public const int HealForceLimit = 8;

    public static bool UsesCashSiteContinuation(GangAction previousAction) =>
        previousAction is GangAction.None or GangAction.Control
            or GangAction.Equip or GangAction.Heal;

    public static bool UsesOpponentContinuation(GangAction previousAction) =>
        previousAction is GangAction.Attack or GangAction.Hide or GangAction.Move;

    public static bool ShouldHeal(int force, int effectiveHeal) =>
        force < HealForceLimit
        && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal;

    public static bool CanAttackSelectedTarget(
        int attackerForce,
        int attackerCombat,
        int attackerDefense,
        int comparisonTargetForce,
        int comparisonTargetCombat,
        int comparisonTargetDefense) =>
        (comparisonTargetForce + comparisonTargetCombat) / 4 - attackerDefense
        <= attackerForce + attackerCombat - comparisonTargetDefense;

    public static int? ThreeMoveTransitionFamily(
        ScenarioId scenario,
        GangAction plannedAction,
        GangAction previousAction,
        GangAction olderAction) =>
        plannedAction == GangAction.Move
        && previousAction == GangAction.Move
        && olderAction == GangAction.Move
            ? scenario == ScenarioId.Siege ? 11 : 2
            : null;

    public static bool ShouldTerminateForGreed(
        ScenarioId scenario,
        int turnsRemaining)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(turnsRemaining);
        return scenario == ScenarioId.Greed && turnsRemaining < 4;
    }

    public static int? SelectHighestCashUnfinishedSite(
        MatchState state,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)sectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));

        var bestCash = 0;
        int? bestSlot = null;
        foreach (var site in state.Sectors[sectorId].Sites.OrderBy(site => site.Slot))
        {
            var cash = state.Definitions.Sites
                .Single(definition => definition.Id == site.DefinitionId).Cash;
            if (site.Resistance <= 0 || cash <= bestCash) continue;
            bestCash = cash;
            bestSlot = site.Slot;
        }
        return bestSlot;
    }

    public static int UnfinishedCashScore(MatchState state, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if ((uint)sectorId >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));

        return state.Sectors[sectorId].Sites
            .Where(site => site.Resistance > 0)
            .Sum(site => Math.Max(0, (int)state.Definitions.Sites
                .Single(definition => definition.Id == site.DefinitionId).Cash));
    }
}
