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

    [Fact]
    public void FormationGroupsEverySixFamilySlotsIncludingInactiveRosterEntries()
    {
        var families = Enumerable.Repeat(
            AiPlanningState.UnusedFamily,
            AiPlanningState.GangSlotsPerPlayer).ToArray();
        foreach (var slot in new[] { 0, 1, 3, 5, 8, 13, 20, 21 })
            families[slot] = 11;

        Assert.Equal(0, OriginalAiFamilyElevenRules.FormationLeaderSlot(families, 0));
        Assert.Equal(0, OriginalAiFamilyElevenRules.FormationLeaderSlot(families, 13));
        Assert.Equal(20, OriginalAiFamilyElevenRules.FormationLeaderSlot(families, 20));
        Assert.Equal(20, OriginalAiFamilyElevenRules.FormationLeaderSlot(families, 21));
    }
}
