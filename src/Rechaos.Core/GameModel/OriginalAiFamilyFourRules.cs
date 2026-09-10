namespace Rechaos.Core.GameModel;

/// <summary>
/// Previous-action and Hide-allocation boundaries recovered from original AI
/// family 4 at 0x00401000.
/// </summary>
internal static class OriginalAiFamilyFourRules
{
    public const int AttackAttemptsAfterHideOrEquip = 5;
    public const int HealForceLimit = 8;
    public const int MaximumPreviousHidesInOwnedSector = 1;

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

    public static bool CanControlAfterRepeatedMove(
        GangAction previousAction,
        GangAction olderAction,
        bool canSoloControl) =>
        previousAction == GangAction.Move
        && olderAction == GangAction.Move
        && canSoloControl;
}
