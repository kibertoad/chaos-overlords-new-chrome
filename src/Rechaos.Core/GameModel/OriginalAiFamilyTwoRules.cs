namespace Rechaos.Core.GameModel;

/// <summary>
/// Aggressive territorial decisions recovered from original AI family 2 at
/// 0x0041fef0.
/// </summary>
internal static class OriginalAiFamilyTwoRules
{
    public const int AttackAttempts = 5;
    public const int HealForceLimit = 8;
    public const int MaximumHealSectorWeight = 4;

    public static bool ShouldHeal(int force, int effectiveHeal, int sectorWeight) =>
        force < HealForceLimit
        && effectiveHeal >= OriginalAiFamilyOneRules.MinimumEffectiveHeal
        && sectorWeight <= MaximumHealSectorWeight;

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

    public static GangAction SelectLocalAction(
        GangAction previousAction,
        ScenarioId scenario,
        bool canSoloControl) =>
        previousAction == GangAction.Control
        || scenario == ScenarioId.Armageddon
        || !canSoloControl
            ? GangAction.Move
            : GangAction.Control;

    public static bool ShouldOverrideWithControl(
        bool ownerIsHostile,
        bool ownerIsHuman,
        int visibleHumanGangCount,
        int visibleOwnerGangCount,
        GangAction previousAction,
        bool hasCombatAdvantageFlag) =>
        ownerIsHostile
        && ((ownerIsHuman
                && visibleHumanGangCount == 0
                && previousAction != GangAction.Control)
            || (visibleOwnerGangCount == 0 && hasCombatAdvantageFlag));

    public static bool ShouldTerminateForGreed(
        ScenarioId scenario,
        int turnsRemaining)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(turnsRemaining);
        return scenario == ScenarioId.Greed && turnsRemaining < 4;
    }
}
