using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilyTwelveRulesTests
{
    [Fact]
    public void EquipmentCooldownUsesRawItemCost() =>
        Assert.Equal(45, OriginalAiFamilyTwelveRules.EquipmentCooldown(45));

    [Theory]
    [InlineData(0, 10, 10, true)]
    [InlineData(-1, 10, 10, true)]
    [InlineData(1, 10, 10, false)]
    [InlineData(0, 11, 10, false)]
    public void EquipmentGateUsesCooldownAndInclusiveCashBoundary(
        int cooldown,
        int itemCost,
        int cash,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyTwelveRules.CanEquip(cooldown, itemCost, cash));

    [Theory]
    [InlineData(9, -3, true)]
    [InlineData(10, -3, false)]
    [InlineData(9, -4, false)]
    public void HealGateUsesRecoveredForceAndEffectiveHealBoundaries(
        int force,
        int effectiveHeal,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyTwelveRules.ShouldHeal(force, effectiveHeal));

    [Theory]
    [InlineData(10, 4, 2, 1, 0, 0, true)]
    [InlineData(1, 0, 0, 10, 4, 2, false)]
    public void AttackComparisonUsesQuarterStrengthFormula(
        int attackerForce,
        int attackerCombat,
        int attackerDefense,
        int targetForce,
        int targetCombat,
        int targetDefense,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyTwelveRules.CanAttackSelectedTarget(
                attackerForce, attackerCombat, attackerDefense,
                targetForce, targetCombat, targetDefense));

    [Theory]
    [InlineData(ScenarioId.Greed, 3, true)]
    [InlineData(ScenarioId.Greed, 4, false)]
    [InlineData(ScenarioId.Siege, 3, false)]
    public void TerminationOverrideUsesFinalThreeGreedTurns(
        ScenarioId scenario,
        int turnsRemaining,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyTwelveRules.ShouldTerminateForGreed(
                scenario, turnsRemaining));
}
