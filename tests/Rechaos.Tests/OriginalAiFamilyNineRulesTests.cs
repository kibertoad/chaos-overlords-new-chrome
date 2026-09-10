using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilyNineRulesTests
{
    [Fact]
    public void EquipmentCooldownIsThreeTimesRawCost() =>
        Assert.Equal(135, OriginalAiFamilyNineRules.EquipmentCooldown(45));

    [Theory]
    [InlineData(true, 0, GangAction.None, GangAction.Move)]
    [InlineData(false, 10, GangAction.None, GangAction.Attack)]
    [InlineData(false, 10, GangAction.Control, GangAction.Attack)]
    [InlineData(false, 1, GangAction.Control, GangAction.Move)]
    [InlineData(false, 0, GangAction.None, GangAction.Control)]
    public void TerritorialActionUsesOwnershipVisibilityAndPreviousControl(
        bool ownsCurrentSector,
        int visibleOpponentWeight,
        GangAction previousAction,
        GangAction expected) =>
        Assert.Equal(expected, OriginalAiFamilyNineRules.SelectTerritorialAction(
            ownsCurrentSector, visibleOpponentWeight, previousAction));

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
        Assert.Equal(expected, OriginalAiFamilyNineRules.CanAttackSelectedTarget(
            attackerForce, attackerCombat, attackerDefense,
            targetForce, targetCombat, targetDefense));
}
