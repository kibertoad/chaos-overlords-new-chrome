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
    [InlineData(GameDuration.SixMonths, 3)]
    [InlineData(GameDuration.OneYear, 6)]
    [InlineData(GameDuration.TwoYears, 13)]
    [InlineData(GameDuration.FourYears, 26)]
    public void GreedStopsHiringAtFinalDurationEighth(
        GameDuration duration,
        int cutoff)
    {
        Assert.False(OriginalAiHireRoleRules.ShouldAttemptHire(
            ScenarioId.Greed, 5, 5, cutoff, duration));
        Assert.True(OriginalAiHireRoleRules.ShouldAttemptHire(
            ScenarioId.Greed, 5, 5, cutoff + 1, duration));
    }

    [Theory]
    [InlineData(ScenarioId.Power)]
    [InlineData(ScenarioId.Acceptance)]
    [InlineData(ScenarioId.Dominance)]
    public void TimedNonGreedScenariosStopWithTwoTurnsRemaining(ScenarioId scenario)
    {
        Assert.False(OriginalAiHireRoleRules.ShouldAttemptHire(
            scenario, 5, 5, 2, GameDuration.OneYear));
        Assert.True(OriginalAiHireRoleRules.ShouldAttemptHire(
            scenario, 5, 5, 3, GameDuration.OneYear));
    }

    [Theory]
    [InlineData(ScenarioId.KillEmAll)]
    [InlineData(ScenarioId.Big40)]
    [InlineData(ScenarioId.Eliminate)]
    [InlineData(ScenarioId.Siege)]
    [InlineData(ScenarioId.Armageddon)]
    public void UntimedBranchesUseInclusiveGangLimit(ScenarioId scenario)
    {
        Assert.True(OriginalAiHireRoleRules.ShouldAttemptHire(
            scenario, 5, 5, 0, GameDuration.OneYear));
        Assert.False(OriginalAiHireRoleRules.ShouldAttemptHire(
            scenario, 6, 5, 0, GameDuration.OneYear));
    }

    [Fact]
    public void BigManBypassesNormalGangLimitGate()
    {
        Assert.True(OriginalAiHireRoleRules.ShouldAttemptHire(
            ScenarioId.BigMan, 81, 0, 0, GameDuration.OneYear));
    }

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
            ? PowerInputs(hasVisibleHostileSector: true)
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

    [Theory]
    [InlineData(9, 3)]
    [InlineData(4, 1)]
    public void EliminateQuotasResetAtExactOneYearBoundary(int turn, int count)
    {
        var inputs = turn == 9
            ? PowerInputs(family5Count: count)
            : PowerInputs(family7Count: count);

        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 0),
            OriginalAiHireRoleRules.SelectEliminateAdjusted(turn, inputs));
    }

    [Fact]
    public void EliminateDurationFactorScalesQuota()
    {
        var result = OriginalAiHireRoleRules.SelectEliminateAdjusted(
            turn: 9,
            PowerInputs(duration: GameDuration.FourYears, family5Count: 11));

        Assert.Equal(new OriginalAiHireRoleSelection(2, 5), result);
    }

    [Fact]
    public void EliminateMinimumBaseFamilyCountOverridesOtherSlots()
    {
        var result = OriginalAiHireRoleRules.SelectEliminateAdjusted(
            turn: 1,
            PowerInputs(family0Or4Count: 3));

        Assert.Equal(new OriginalAiHireRoleSelection(0, 0), result);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(8)]
    public void SiegeFamilyThreeQuotaResetsScheduledSlots(int turn)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectSiegeAdjusted(
                turn,
                PowerInputs(family3Count: 4, family6Or12Count: 1)));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    public void SiegeFamilySixQuotaResetsScheduledSlots(int turn)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectSiegeAdjusted(
                turn,
                PowerInputs(family6Or12Count: 6)));
    }

    [Theory]
    [InlineData(2, 100)]
    [InlineData(1, 99)]
    public void SiegeSlotNineChecksFamilyAndCashThresholds(int family2Count, int cash)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectSiegeAdjusted(
                turn: 9,
                PowerInputs(cash: cash, family2Count: family2Count, family6Or12Count: 1)));
    }

    [Fact]
    public void SiegeSlotNineKeepsExactCashBoundaryBelowFamilyQuota()
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(5, 3),
            OriginalAiHireRoleRules.SelectSiegeAdjusted(
                turn: 9,
                PowerInputs(cash: 100, family2Count: 1, family6Or12Count: 1)));
    }

    [Fact]
    public void SiegeRequiresAtLeastOneFamilySixOrTwelveGang()
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(3, 4),
            OriginalAiHireRoleRules.SelectSiegeAdjusted(
                turn: 1,
                PowerInputs(family0Or4Count: 5, family6Or12Count: 0)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(7)]
    public void BigManRedirectsBaseSlotsOnlyAboveFiveBaseFamilyGangs(int turn)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(1, 1),
            OriginalAiHireRoleRules.SelectBigManAdjusted(
                turn,
                PowerInputs(family0Or4Count: 6)));
    }

    [Fact]
    public void BigManKeepsSlotAtExactFiveBoundary()
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(2, 3),
            OriginalAiHireRoleRules.SelectBigManAdjusted(
                turn: 5,
                PowerInputs(family0Or4Count: 5)));
    }

    [Theory]
    [InlineData(0, 2, 5)]
    [InlineData(1, 3, 3)]
    public void ArmageddonSlotFiveRedirectsByFamilyThreePresence(
        int family3Count,
        int expectedMode,
        int expectedRole)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(expectedMode, expectedRole),
            OriginalAiHireRoleRules.SelectArmageddonAdjusted(
                turn: 5,
                PowerInputs(family2Count: 1, family3Count: family3Count)));
    }

    [Fact]
    public void ArmageddonForcesSlotFiveWhenSectorConditionIsClear()
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(3, 4),
            OriginalAiHireRoleRules.SelectArmageddonAdjusted(
                turn: 0,
                PowerInputs(
                    hasVisibleHostileSector: true,
                    hasFamily6CoveringFirstHostileSector: false,
                    family2Count: 1,
                    family3Count: 1,
                    family6Or12Count: 1)));
    }

    [Theory]
    [InlineData(3, 2, 4)]
    [InlineData(5, 6, 4)]
    [InlineData(9, 3, 3)]
    public void ArmageddonQuotasResetAtExactOneYearBoundary(
        int turn,
        int countedFamily,
        int boundary)
    {
        var inputs = PowerInputs(family2Count: 1, family3Count: 1);
        inputs = countedFamily switch
        {
            2 => inputs with { Family2Count = boundary },
            3 => inputs with { Family3Count = boundary },
            6 => inputs with
            {
                HasVisibleHostileSector = true,
                HasFamily6CoveringFirstHostileSector = false,
                Family6Or12Count = boundary
            },
            _ => throw new ArgumentOutOfRangeException(nameof(countedFamily))
        };

        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 0),
            OriginalAiHireRoleRules.SelectArmageddonAdjusted(turn, inputs));
    }

    [Fact]
    public void ArmageddonMissingFamilyTwoIsFinalOverride()
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(3, 3),
            OriginalAiHireRoleRules.SelectArmageddonAdjusted(
                turn: 0,
                PowerInputs(family2Count: 0, family0Or4Count: 3)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(8)]
    public void GreedLateTurnsResetResearchScheduleSlots(int turn)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectGreedAdjusted(
                turn,
                PowerInputs(turnsRemaining: 9)));
    }

    [Theory]
    [InlineData(0, 1, 4, 6)]
    [InlineData(1, 0, 2, 2)]
    [InlineData(1, 1, 3, 3)]
    public void GreedSlotFiveRedirectsByMissingFamilies(
        int family7Count,
        int family3Count,
        int expectedMode,
        int expectedRole)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(expectedMode, expectedRole),
            OriginalAiHireRoleRules.SelectGreedAdjusted(
                turn: 5,
                PowerInputs(family7Count: family7Count, family3Count: family3Count)));
    }

    [Fact]
    public void GreedClearSectorConditionForcesSlotFive()
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(3, 4),
            OriginalAiHireRoleRules.SelectGreedAdjusted(
                turn: 0,
                PowerInputs(
                    hasVisibleHostileSector: true,
                    hasFamily6CoveringFirstHostileSector: false,
                    family3Count: 1,
                    family6Or12Count: 1)));
    }

    [Fact]
    public void GreedTrailingPlayerAppliesExtraSixMonthCashFloor()
    {
        var inputs = PowerInputs(
            cash: 75,
            hasHigherScoringPlayer: false,
            family2Count: 0,
            duration: GameDuration.SixMonths);

        Assert.Equal(
            new OriginalAiHireRoleSelection(3, 3),
            OriginalAiHireRoleRules.SelectGreedAdjusted(turn: 9, inputs));
        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectGreedAdjusted(
                turn: 9,
                inputs with { HasHigherScoringPlayer = true }));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void AcceptanceFinalFourTurnsResetSpecializedSlots(int turn)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectAcceptanceAdjusted(
                turn,
                PowerInputs(turnsRemaining: 4)));
    }

    [Theory]
    [InlineData(0, 1, 4, 6)]
    [InlineData(1, 0, 2, 2)]
    [InlineData(1, 1, 3, 3)]
    public void AcceptanceSlotTwoRedirectsByMissingFamilies(
        int family7Count,
        int family5Count,
        int expectedMode,
        int expectedRole)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(expectedMode, expectedRole),
            OriginalAiHireRoleRules.SelectAcceptanceAdjusted(
                turn: 2,
                PowerInputs(family7Count: family7Count, family5Count: family5Count)));
    }

    [Fact]
    public void AcceptanceClearSectorConditionForcesSlotTwo()
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(3, 4),
            OriginalAiHireRoleRules.SelectAcceptanceAdjusted(
                turn: 0,
                PowerInputs(
                    hasVisibleHostileSector: true,
                    hasFamily6CoveringFirstHostileSector: false,
                    family5Count: 1,
                    family6Or12Count: 1)));
    }

    [Theory]
    [InlineData(4, 5, 6)]
    [InlineData(5, 2, 2)]
    [InlineData(2, 6, 3)]
    [InlineData(1, 7, 1)]
    public void AcceptanceQuotasResetAtExactOneYearBoundary(
        int turn,
        int countedFamily,
        int boundary)
    {
        var inputs = PowerInputs();
        inputs = countedFamily switch
        {
            2 => inputs with { Family2Count = boundary },
            5 => inputs with { Family5Count = boundary },
            6 => inputs with
            {
                HasVisibleHostileSector = true,
                HasFamily6CoveringFirstHostileSector = false,
                Family6Or12Count = boundary
            },
            7 => inputs with { Family7Count = boundary },
            _ => throw new ArgumentOutOfRangeException(nameof(countedFamily))
        };

        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectAcceptanceAdjusted(turn, inputs));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    public void DominanceFinalSevenTurnsResetSpecializedSlots(int turn)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectDominanceAdjusted(
                turn,
                PowerInputs(turnsRemaining: 7)));
    }

    [Theory]
    [InlineData(0, 1, 1, 4, 6)]
    [InlineData(1, 0, 1, 2, 5)]
    [InlineData(1, 1, 0, 2, 2)]
    [InlineData(1, 1, 1, 3, 3)]
    public void DominanceSlotTenRedirectsByMissingFamilies(
        int family7Count,
        int family3Count,
        int family5Count,
        int expectedMode,
        int expectedRole)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(expectedMode, expectedRole),
            OriginalAiHireRoleRules.SelectDominanceAdjusted(
                turn: 10,
                PowerInputs(
                    family7Count: family7Count,
                    family3Count: family3Count,
                    family5Count: family5Count)));
    }

    [Fact]
    public void DominanceClearSectorConditionForcesUniqueSlotTen()
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(3, 4),
            OriginalAiHireRoleRules.SelectDominanceAdjusted(
                turn: 0,
                PowerInputs(
                    hasVisibleHostileSector: true,
                    hasFamily6CoveringFirstHostileSector: false,
                    family3Count: 1)));
    }

    [Theory]
    [InlineData(3, 5, 3)]
    [InlineData(2, 3, 3)]
    [InlineData(5, 2, 2)]
    [InlineData(10, 6, 3)]
    [InlineData(4, 7, 1)]
    public void DominanceQuotasResetAtExactOneYearBoundary(
        int turn,
        int countedFamily,
        int boundary)
    {
        var inputs = countedFamily == 6
            ? PowerInputs(
                    hasVisibleHostileSector: true,
                    hasFamily6CoveringFirstHostileSector: false,
                family3Count: 1,
                family6Or12Count: boundary)
            : PowerInputs();
        inputs = countedFamily switch
        {
            2 => inputs with { Family2Count = boundary },
            3 => inputs with { Family3Count = boundary },
            5 => inputs with { Family5Count = boundary },
            7 => inputs with { Family7Count = boundary },
            _ => inputs
        };

        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectDominanceAdjusted(turn, inputs));
    }

    [Fact]
    public void DominanceMinimumBaseFamilyCountOverridesOtherSlots()
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(0, 1),
            OriginalAiHireRoleRules.SelectDominanceAdjusted(
                turn: 1,
                PowerInputs(family0Or4Count: 3)));
    }

    [Theory]
    [InlineData(ScenarioId.Greed, 5, 2, 2)]
    [InlineData(ScenarioId.Power, 6, 3, 3)]
    [InlineData(ScenarioId.Acceptance, 2, 3, 3)]
    [InlineData(ScenarioId.Dominance, 10, 2, 5)]
    [InlineData(ScenarioId.KillEmAll, 6, 3, 3)]
    [InlineData(ScenarioId.Big40, 6, 3, 3)]
    [InlineData(ScenarioId.Eliminate, 9, 2, 5)]
    [InlineData(ScenarioId.Siege, 1, 3, 4)]
    [InlineData(ScenarioId.BigMan, 5, 2, 3)]
    [InlineData(ScenarioId.Armageddon, 5, 3, 3)]
    public void AdjustedDispatcherCoversEveryScenario(
        ScenarioId scenario,
        int turn,
        int expectedMode,
        int expectedRole)
    {
        Assert.Equal(
            new OriginalAiHireRoleSelection(expectedMode, expectedRole),
            OriginalAiHireRoleRules.SelectAdjusted(scenario, turn, PowerInputs()));
    }

    private static OriginalAiHireAdjustmentInputs PowerInputs(
        int turnsRemaining = 52,
        int cash = 100,
        bool hasHigherScoringPlayer = false,
        bool hasVisibleHostileSector = false,
        bool hasFamily6CoveringFirstHostileSector = false,
        int family5Count = 1,
        int family7Count = 1,
        int previousRole = 0,
        int family2Count = 0,
        int family3Count = 0,
        int family6Or12Count = 0,
        int family0Or4Count = 5,
        GameDuration duration = GameDuration.OneYear) =>
        new(turnsRemaining, cash, hasHigherScoringPlayer, hasVisibleHostileSector,
            hasFamily6CoveringFirstHostileSector,
            family5Count, family7Count, previousRole, family2Count,
            family3Count, family6Or12Count, family0Or4Count, duration);
}
