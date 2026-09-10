namespace Rechaos.Core.GameModel;

/// <summary>
/// Coverage, combat, healing, and end-game rules recovered from original AI
/// family 6 at 0x00431c60.
/// </summary>
internal static class OriginalAiFamilySixRules
{
    public const int RetryAttackAttempts = 5;
    public const int HealForceLimit = 8;

    public static bool ShouldHeal(
        int force,
        int effectiveHeal,
        GangAction previousAction,
        int visibleOpponentWeight) =>
        force < HealForceLimit
        && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal
        && previousAction != GangAction.Attack
        && visibleOpponentWeight == 0;

    public static bool CanAttackSelectedTarget(
        int attackerForce,
        int attackerCombat,
        int attackerDefense,
        int comparisonTargetForce,
        int comparisonTargetCombat,
        int comparisonTargetDefense) =>
        OriginalAiFamilyTwelveRules.CanAttackSelectedTarget(
            attackerForce, attackerCombat, attackerDefense,
            comparisonTargetForce, comparisonTargetCombat,
            comparisonTargetDefense);

    public static bool ShouldTerminateForGreed(
        ScenarioId scenario,
        int turnsRemaining) =>
        OriginalAiFamilyTwelveRules.ShouldTerminateForGreed(
            scenario, turnsRemaining);
}
