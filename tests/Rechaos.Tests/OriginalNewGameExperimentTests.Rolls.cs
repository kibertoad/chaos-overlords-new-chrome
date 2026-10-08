using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> DivergingRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var ((experiment, run), _) in KnownDivergences) data.Add(experiment, run);
        return data;
    }

    // A known divergence stays where it was found; when a fix moves it, its KnownDivergences entry goes.
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(DivergingRuns))]
    public void AKnownDivergenceIsStillWhereItWasFound(string experiment, int run) =>
        Assert.Equal(KnownDivergences[(experiment, run)], FirstDifferingRoll(Run(experiment, run)));

    // DEV-AI-007 on: in turn 5 player 5's gang in sector 9 is given Move to sector 5, which the
    // original carries out and the rebuild refuses; player 5's next tie count differs (EXP-TURN-015).
    [Fact]
    public void WithDevAi007OnExpTurn015PartsAtTheRefusedMove() =>
        Assert.Equal(950, FirstDifferingRoll(Run("EXP-TURN-015", 0), computerMovesToNeighboursOnly: true));

    // DEV-AI-008 on: in turn 20 player 3 hires into sector 36, which it neither controls nor holds
    // a gang in; the original rolls the new gang's Force and the rebuild drops the hire (EXP-TURN-090).
    [Fact]
    public void WithDevAi008OnExpTurn090PartsAtTheDroppedHire() =>
        Assert.Equal(3335, FirstDifferingRoll(Run("EXP-TURN-090", 0), computerHiresWhereHumansCan: true));

    private static int FirstDifferingRoll(
        RecordedRun recorded, bool computerMovesToNeighboursOnly = false, bool computerHiresWhereHumansCan = false)
    {
        var rolls = ObservingRolls(() => StartMatch(recorded, out _,
            computerMovesToNeighboursOnly: computerMovesToNeighboursOnly,
            computerHiresWhereHumansCan: computerHiresWhereHumansCan));

        return Enumerable.Range(0, Math.Min(rolls.Count, recorded.Rolls.Count))
            .First(index => rolls[index] != (recorded.Rolls[index].Bound, recorded.Rolls[index].Result));
    }

    // RULE-RNG-001, RULE-RNG-002: every result the original returned follows from the seed.
    [Theory]
    [MemberData(nameof(Runs))]
    public void EveryRollFollowsFromTheSeed(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var random = new DeterministicRandom(recorded.Seed);
        foreach (var (call, bound, result) in recorded.Rolls)
            Assert.True(random.NextInclusive(bound) == result, $"roll({bound}) at {call}");
    }
}
