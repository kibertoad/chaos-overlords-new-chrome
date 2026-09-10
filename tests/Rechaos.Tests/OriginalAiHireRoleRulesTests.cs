using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiHireRoleRulesTests
{
    public static TheoryData<ScenarioId, (int RankingMode, int Role)[]> Schedules => new()
    {
        { ScenarioId.Greed, [(0, 1), (4, 6), (0, 1), (2, 2), (0, 1), (3, 4), (2, 2), (0, 1), (2, 2), (3, 3)] },
        { ScenarioId.Power, [(0, 0), (1, 1), (0, 0), (0, 0), (4, 6), (0, 0), (3, 4), (0, 0), (3, 3), (2, 5)] },
        { ScenarioId.Acceptance, [(0, 1), (4, 6), (3, 4), (0, 1), (2, 2), (3, 3), (2, 2), (0, 1), (2, 2), (0, 1)] },
        { ScenarioId.Dominance, [(0, 1), (0, 1), (2, 5), (2, 2), (4, 6), (3, 3), (2, 2), (0, 1), (2, 5), (0, 1), (3, 4)] },
        { ScenarioId.KillEmAll, [(0, 0), (1, 1), (0, 0), (0, 0), (4, 6), (0, 0), (3, 4), (0, 0), (3, 3), (2, 5)] },
        { ScenarioId.Big40, [(0, 0), (1, 1), (0, 0), (0, 0), (4, 6), (0, 0), (3, 4), (0, 0), (3, 3), (2, 5)] },
        { ScenarioId.Eliminate, [(0, 0), (1, 2), (0, 0), (1, 2), (4, 6), (1, 1), (1, 2), (0, 0), (1, 1), (2, 5)] },
        { ScenarioId.Siege, [(0, 1), (4, 6), (0, 1), (2, 2), (0, 1), (3, 4), (2, 2), (3, 4), (2, 2), (5, 3)] },
        { ScenarioId.BigMan, [(0, 0), (1, 2), (0, 0), (1, 1), (1, 1), (2, 3), (1, 2), (0, 0), (1, 1), (1, 2)] },
        { ScenarioId.Armageddon, [(0, 0), (1, 1), (0, 0), (3, 3), (0, 0), (3, 4), (0, 0), (3, 3), (0, 0), (2, 5)] }
    };

    [Theory]
    [MemberData(nameof(Schedules))]
    public void MatchesEveryOriginalScheduleSlot(
        ScenarioId scenario,
        (int RankingMode, int Role)[] expected)
    {
        for (var turn = 0; turn < expected.Length; turn++)
        {
            var selection = OriginalAiHireRoleRules.SelectScheduled(scenario, turn);
            Assert.Equal(expected[turn].RankingMode, selection.RankingMode);
            Assert.Equal(expected[turn].Role, selection.Role);
        }
    }

    [Theory]
    [MemberData(nameof(Schedules))]
    public void RepeatsAtScenarioSpecificPeriod(
        ScenarioId scenario,
        (int RankingMode, int Role)[] expected)
    {
        Assert.Equal(
            OriginalAiHireRoleRules.SelectScheduled(scenario, 0),
            OriginalAiHireRoleRules.SelectScheduled(scenario, expected.Length));
    }

    [Fact]
    public void RejectsNegativeTurns()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OriginalAiHireRoleRules.SelectScheduled(ScenarioId.Greed, -1));
    }
}
