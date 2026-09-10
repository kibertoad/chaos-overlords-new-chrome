using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilyTwoRulesTests
{
    [Theory]
    [InlineData(7, -3, 4, true)]
    [InlineData(8, -3, 4, false)]
    [InlineData(7, -4, 4, false)]
    [InlineData(7, -3, 5, false)]
    public void HealUsesForceStatAndSectorWeightBoundaries(
        int force,
        int heal,
        int sectorWeight,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyTwoRules.ShouldHeal(force, heal, sectorWeight));

    [Theory]
    [InlineData(GangAction.None, ScenarioId.Power, true, GangAction.Control)]
    [InlineData(GangAction.Control, ScenarioId.Power, true, GangAction.Move)]
    [InlineData(GangAction.None, ScenarioId.Armageddon, true, GangAction.Move)]
    [InlineData(GangAction.None, ScenarioId.Power, false, GangAction.Move)]
    public void LocalActionUsesPreviousControlArmageddonAndStrengthGates(
        GangAction previous,
        ScenarioId scenario,
        bool canSoloControl,
        GangAction expected) =>
        Assert.Equal(expected, OriginalAiFamilyTwoRules.SelectLocalAction(
            previous, scenario, canSoloControl));

    [Theory]
    [InlineData(true, true, 0, 1, GangAction.None, false, true)]
    [InlineData(true, true, 0, 1, GangAction.Control, false, false)]
    [InlineData(true, false, 1, 0, GangAction.None, true, true)]
    [InlineData(true, false, 1, 0, GangAction.None, false, false)]
    [InlineData(false, true, 0, 0, GangAction.None, true, false)]
    public void LateControlOverridePreservesBothOriginalGates(
        bool hostile,
        bool human,
        int visibleHumans,
        int visibleOwnerGangs,
        GangAction previous,
        bool advantage,
        bool expected) =>
        Assert.Equal(expected, OriginalAiFamilyTwoRules.ShouldOverrideWithControl(
            hostile, human, visibleHumans, visibleOwnerGangs, previous, advantage));

    [Theory]
    [InlineData(ScenarioId.Greed, 3, true)]
    [InlineData(ScenarioId.Greed, 4, false)]
    [InlineData(ScenarioId.Power, 3, false)]
    public void GreedTerminationUsesFinalThreeTurns(
        ScenarioId scenario,
        int turnsRemaining,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiFamilyTwoRules.ShouldTerminateForGreed(scenario, turnsRemaining));
}
