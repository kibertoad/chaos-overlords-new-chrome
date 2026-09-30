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

    /// <summary>
    /// FND-AI-070: the cost is compared with cash as signed values (<c>JG</c> at 0x00435684 and
    /// 0x004357D2), so a player whose upkeep left its cash below 0 (RULE-UPKEEP-001) fails the test.
    /// </summary>
    public static bool CanEquip(int cooldown, int itemCost, int cash)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCost);
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
