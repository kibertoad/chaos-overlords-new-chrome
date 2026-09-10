namespace Rechaos.Core.GameModel;

/// <summary>
/// Defensive equipment, healing, movement, and attack choices recovered from
/// original AI family 12 at 0x004353a0.
/// </summary>
internal static class OriginalAiFamilyTwelveRules
{
    public const int AttackAttempts = 5;
    public const int HealForceLimit = 10;

    public static int EquipmentCooldown(int itemCost)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCost);
        return itemCost;
    }

    public static bool CanEquip(int cooldown, int itemCost, int cash)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCost);
        ArgumentOutOfRangeException.ThrowIfNegative(cash);
        return cooldown <= 0 && itemCost <= cash;
    }

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

    public static bool ShouldTerminateForGreed(
        ScenarioId scenario,
        int turnsRemaining)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(turnsRemaining);
        return scenario == ScenarioId.Greed && turnsRemaining < 4;
    }
}
