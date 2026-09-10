using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiObjectiveFamilyRulesTests
{
    [Theory]
    [InlineData(ScenarioId.BigMan, 13, 12)]
    [InlineData(ScenarioId.Eliminate, 13, 13)]
    [InlineData(ScenarioId.BigMan, 14, 14)]
    [InlineData(ScenarioId.Eliminate, 14, 15)]
    public void FamiliesMapToTheirExactObjectiveModes(
        ScenarioId scenario,
        int family,
        int expectedMode) =>
        Assert.Equal(expectedMode,
            OriginalAiObjectiveFamilyRules.SelectionMode(scenario, family));

    [Theory]
    [InlineData(ScenarioId.BigMan, 27, true)]
    [InlineData(ScenarioId.BigMan, 36, true)]
    [InlineData(ScenarioId.BigMan, 9, false)]
    [InlineData(ScenarioId.Eliminate, 9, true)]
    [InlineData(ScenarioId.Eliminate, 54, true)]
    [InlineData(ScenarioId.Eliminate, 27, false)]
    [InlineData(ScenarioId.Power, 27, false)]
    public void SelectorOneFRecognizesOnlyScenarioObjectives(
        ScenarioId scenario,
        int sector,
        bool expected) =>
        Assert.Equal(expected,
            OriginalAiObjectiveFamilyRules.IsObjectiveSector(scenario, sector));

    [Fact]
    public void OffObjectiveTerminalOverridePreservesOnlyEquip()
    {
        Assert.True(OriginalAiObjectiveFamilyRules.ShouldOverrideWithMove(
            ScenarioId.BigMan, 0, GangAction.Attack));
        Assert.True(OriginalAiObjectiveFamilyRules.ShouldOverrideWithMove(
            ScenarioId.Eliminate, 0, GangAction.None));
        Assert.False(OriginalAiObjectiveFamilyRules.ShouldOverrideWithMove(
            ScenarioId.BigMan, 0, GangAction.Equip));
        Assert.False(OriginalAiObjectiveFamilyRules.ShouldOverrideWithMove(
            ScenarioId.BigMan, 27, GangAction.Attack));
    }
}
