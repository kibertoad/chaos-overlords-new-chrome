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

    [Theory]
    [InlineData(ScenarioId.Power)]
    [InlineData(ScenarioId.KillEmAll)]
    [InlineData(ScenarioId.Big40)]
    public void PowerFamilyScenariosShareAdjustedBranch(ScenarioId scenario)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(1, 1),
            OriginalAiHireRoleRules.SelectPowerAdjusted(scenario, turn: 4,
                PowerInputs(turnsRemaining: 9)));
    }

    [Theory]
    [InlineData(0, 1, 4, 6)]
    [InlineData(1, 0, 2, 5)]
    [InlineData(1, 1, 3, 3)]
    public void PowerSlotSixRedirectsToMissingOrFallbackFamily(
        int family7Count,
        int family5Count,
        int expectedMode,
        int expectedRole)
    {
        var result = OriginalAiHireRoleRules.SelectPowerAdjusted(
            ScenarioId.Power,
            turn: 6,
            PowerInputs(
                selector9aResult: 100,
                family7Count: family7Count,
                family5Count: family5Count));

        Assert.Equal(new OriginalAiHireRoleSelection(expectedMode, expectedRole), result);
    }

    [Theory]
    [InlineData(8, 2, 4)]
    [InlineData(6, 6, 4)]
    [InlineData(9, 5, 3)]
    [InlineData(4, 7, 1)]
    public void PowerQuotasResetSlotAtExactOneYearBoundary(
        int turn,
        int countedFamily,
        int boundary)
    {
        var inputs = countedFamily == 6
            ? PowerInputs(selector9aResult: 0, selector5fResult: -1)
            : PowerInputs();
        inputs = countedFamily switch
        {
            2 => inputs with { Family2Count = boundary },
            5 => inputs with { Family5Count = boundary },
            6 => inputs with { Family6Or12Count = boundary },
            7 => inputs with { Family7Count = boundary },
            _ => throw new ArgumentOutOfRangeException(nameof(countedFamily))
        };

        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 0),
            OriginalAiHireRoleRules.SelectPowerAdjusted(ScenarioId.Power, turn, inputs));
    }

    [Fact]
    public void PowerDurationFactorScalesQuota()
    {
        var result = OriginalAiHireRoleRules.SelectPowerAdjusted(
            ScenarioId.Power,
            turn: 8,
            PowerInputs(duration: GameDuration.FourYears, family2Count: 15));

        Assert.Equal(new OriginalAiHireRoleSelection(3, 3), result);
    }

    [Fact]
    public void PowerMinimumBaseFamilyCountOverridesOtherSlots()
    {
        var result = OriginalAiHireRoleRules.SelectPowerAdjusted(
            ScenarioId.Power,
            turn: 1,
            PowerInputs(family0Or4Count: 3));

        Assert.Equal(new OriginalAiHireRoleSelection(0, 0), result);
    }

    private static OriginalAiPowerHireInputs PowerInputs(
        int turnsRemaining = 52,
        int cash = 100,
        int selector9aResult = 100,
        int selector5fResult = -1,
        int family5Count = 1,
        int family7Count = 1,
        int previousRole = 0,
        int family2Count = 0,
        int family6Or12Count = 0,
        int family0Or4Count = 4,
        GameDuration duration = GameDuration.OneYear) =>
        new(turnsRemaining, cash, selector9aResult, selector5fResult,
            family5Count, family7Count, previousRole, family2Count,
            family6Or12Count, family0Or4Count, duration);
}
