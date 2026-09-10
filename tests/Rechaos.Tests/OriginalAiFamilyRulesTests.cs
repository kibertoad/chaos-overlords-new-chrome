using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiFamilyRulesTests
{
    private const int Preserved = 99;

    public static TheoryData<ScenarioId, int[]> Tables => new()
    {
        { ScenarioId.Greed,       [0, 0, 3, 2, 6, Preserved, 7] },
        { ScenarioId.Power,       [0, 1, 3, 2, 6, 5, 7] },
        { ScenarioId.Acceptance,  [0, 0, 5, 2, 6, Preserved, 7] },
        { ScenarioId.Dominance,   [0, 0, 5, 2, 6, 3, 7] },
        { ScenarioId.KillEmAll,   [0, 1, 3, 2, 6, 5, 7] },
        { ScenarioId.Big40,       [0, 1, 3, 2, 6, 5, 7] },
        { ScenarioId.Eliminate,   [0, 13, 14, Preserved, 6, 5, 7] },
        { ScenarioId.Siege,       [10, 0, 3, 11, 12, Preserved, 7] },
        { ScenarioId.BigMan,      [0, 13, 14, 3, Preserved, Preserved, Preserved] },
        { ScenarioId.Armageddon,  [0, 1, 3, 2, 6, 3, Preserved] }
    };

    [Theory]
    [MemberData(nameof(Tables))]
    public void ScenarioAndStrategicModeSelectOriginalFamily(
        ScenarioId scenario,
        int[] expected)
    {
        Assert.Equal(7, expected.Length);

        for (var strategicMode = 0; strategicMode < expected.Length; strategicMode++)
        {
            var selection = OriginalAiFamilyRules.Select(scenario, strategicMode, Preserved);

            Assert.Equal(expected[strategicMode], selection.Family);
            Assert.Equal(strategicMode == 4 && expected[strategicMode] != Preserved,
                selection.CopiesProjectedGangValue);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(100)]
    public void UnknownStrategicModePreservesCurrentFamily(int strategicMode)
    {
        var selection = OriginalAiFamilyRules.Select(ScenarioId.Power, strategicMode, 11);

        Assert.Equal(11, selection.Family);
        Assert.False(selection.CopiesProjectedGangValue);
    }
}
