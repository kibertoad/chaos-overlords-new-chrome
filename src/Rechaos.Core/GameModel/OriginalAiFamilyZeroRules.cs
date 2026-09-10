namespace Rechaos.Core.GameModel;

/// <summary>
/// Previous-action state-machine boundaries recovered from original AI family
/// 0 at 0x00428ef0.
/// </summary>
internal static class OriginalAiFamilyZeroRules
{
    public const int AttackAttemptsAfterHideOrEquip = 5;
    public const int HealForceLimit = 8;

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
        OriginalAiFamilyTwelveRules.CanAttackSelectedTarget(
            attackerForce, attackerCombat, attackerDefense,
            comparisonTargetForce, comparisonTargetCombat,
            comparisonTargetDefense);

    public static int? FamilyAfterPlanning(
        ScenarioId scenario,
        GangAction plannedAction,
        GangAction olderAction) =>
        plannedAction == GangAction.Move && olderAction == GangAction.Move
            ? scenario == ScenarioId.Siege ? 11 : 2
            : null;
}
