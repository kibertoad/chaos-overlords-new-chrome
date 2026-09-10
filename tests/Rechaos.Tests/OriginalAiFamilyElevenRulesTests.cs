using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilyElevenRulesTests
{
    [Theory]
    [InlineData(7, -3, GangAction.None, true)]
    [InlineData(8, -3, GangAction.None, false)]
    [InlineData(7, -4, GangAction.None, false)]
    [InlineData(7, -3, GangAction.Attack, false)]
    [InlineData(7, -3, GangAction.Control, true)]
    public void HealGatePreservesForceStatisticAndPreviousAttackBoundaries(
        int force,
        int effectiveHeal,
        GangAction previousAction,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyElevenRules.ShouldHeal(
                force, effectiveHeal, previousAction));
}
