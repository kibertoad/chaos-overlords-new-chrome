using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiHirePlacementRulesTests
{
    [Fact]
    public void ModeOneDrawsFromOwnedSectorsThenGangSlotsWithDuplicatesRetained()
    {
        var facts = new Facts(seed: 1);
        facts.Owners[2] = 0;
        facts.Owners[9] = 0;
        facts.Gangs[0] = 9;
        facts.Gangs[1] = 9;
        facts.Gangs[2] = 4;
        int[] exactMultiset = [2, 9, 9, 9, 4];
        var expectedRandom = facts.CloneRandom();
        var expected = exactMultiset[expectedRandom.NextInclusive(exactMultiset.Length) - 1];

        var result = facts.Select(mode: 1);

        Assert.True(result.WritesDestination);
        Assert.Equal(expected, result.TargetSectorId);
        Assert.Equal(expectedRandom.State, facts.Random.State);
        Assert.Equal(3, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void ModeOneDoesNotApplyTheSixGangSectorCapacity()
    {
        var facts = new Facts(seed: 1);
        for (var slot = 0; slot < 7; slot++) facts.Gangs[slot] = 22;

        var result = facts.Select(mode: 1);

        Assert.Equal(22, result.TargetSectorId);
        Assert.Equal(3, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void EmptyModeOneClampsBoundConsumesDrawAndReturnsMinusOne()
    {
        var facts = new Facts(seed: 1);

        var result = facts.Select(mode: 1);

        Assert.Equal(new OriginalAiHirePlacementResult(-1, true), result);
        Assert.Equal(3, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void ModeZeroConsumesExtremePredrawThenFallsThroughAndOverwritesTarget()
    {
        var facts = new Facts(seed: 4321);
        facts.Owners[11] = 0;
        facts.Gangs[0] = 3;
        facts.Gangs[1] = 40;
        var expectedRandom = facts.CloneRandom();
        _ = expectedRandom.NextInclusive(2);
        int[] modeOneMultiset = [11, 3, 40];
        var expected = modeOneMultiset[
            expectedRandom.NextInclusive(modeOneMultiset.Length) - 1];

        var result = facts.Select(mode: 0, offerSlot: 0);

        Assert.Equal(expected, result.TargetSectorId);
        Assert.True(result.WritesDestination);
        Assert.Equal(expectedRandom.State, facts.Random.State);
        Assert.Equal(6, facts.Random.ConsumptionCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-8)]
    public void ModeZeroWithNegativeOfferSlotSkipsExtremePredraw(int offerSlot)
    {
        var facts = new Facts(seed: 4321);
        facts.Owners[11] = 0;
        facts.Gangs[0] = 3;

        _ = facts.Select(mode: 0, offerSlot);

        Assert.Equal(3, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void ModeZeroSkipsPredrawWhenBothExtremeMultiplicitiesAreAtLeastSix()
    {
        var facts = new Facts(seed: 4321);
        for (var slot = 0; slot < 6; slot++) facts.Gangs[slot] = 3;
        for (var slot = 6; slot < 12; slot++) facts.Gangs[slot] = 40;

        _ = facts.Select(mode: 0, offerSlot: 2);

        Assert.Equal(3, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void ModeZeroPredrawsWhenOnlyOneExtremeIsBelowSix()
    {
        var facts = new Facts(seed: 4321);
        for (var slot = 0; slot < 6; slot++) facts.Gangs[slot] = 3;
        facts.Gangs[6] = 40;

        _ = facts.Select(mode: 0, offerSlot: 2);

        Assert.Equal(6, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void ModeZeroTreatsOneSectorWithSixGangsAsBothFullExtremes()
    {
        var facts = new Facts(seed: 4321);
        for (var slot = 0; slot < 6; slot++) facts.Gangs[slot] = 3;

        _ = facts.Select(mode: 0, offerSlot: 0);

        Assert.Equal(3, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void EmptyModeZeroConsumesPredrawAndClampedFallthroughDraw()
    {
        var facts = new Facts(seed: 4321);

        var result = facts.Select(mode: 0, offerSlot: 0);

        Assert.Equal(-1, result.TargetSectorId);
        Assert.Equal(6, facts.Random.ConsumptionCount);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(17)]
    [InlineData(63)]
    public void ModesTwoThroughSixtyThreeReturnNinetyNineWithoutWriteOrRng(int mode)
    {
        var random = new DeterministicRandom(77);
        var state = random.State;

        var result = OriginalAiHirePlacementRules.Select(
            new PlayerId(0), mode, 0, [], [], random);

        Assert.Equal(new OriginalAiHirePlacementResult(99, false), result);
        Assert.Equal(state, random.State);
        Assert.Equal(0, random.ConsumptionCount);
    }

    [Theory]
    [InlineData(64, 0)]
    [InlineData(99, 35)]
    [InlineData(128, 64)]
    public void ModesAtLeastSixtyFourWriteLiteralOffsetWithoutValidationOrRng(
        int mode,
        int expected)
    {
        var result = OriginalAiHirePlacementRules.Select(
            new PlayerId(0), mode, 0, null!, null!, null!);

        Assert.Equal(new OriginalAiHirePlacementResult(expected, true), result);
    }

    [Fact]
    public void SelectionModesRejectMalformedExplicitFacts()
    {
        var facts = new Facts();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            facts.Select(mode: -1));
        Assert.Throws<ArgumentException>(() => OriginalAiHirePlacementRules.Select(
            new PlayerId(0), 1, 0,
            facts.Owners[..^1], facts.Gangs, facts.Random));
        facts.Gangs[0] = 99;
        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 1));
    }

    private sealed class Facts
    {
        public Facts(int seed = 1)
        {
            Owners = Enumerable.Repeat(-1, MatchLimits.SectorCount).ToArray();
            Gangs = Enumerable.Repeat(
                OriginalAiHirePlacementRules.InactiveGangSector,
                OriginalAiHirePlacementRules.OriginalGangSlotCount).ToArray();
            Random = new DeterministicRandom(seed);
        }

        public int[] Owners { get; }
        public int[] Gangs { get; }
        public DeterministicRandom Random { get; }

        public DeterministicRandom CloneRandom() =>
            new(Random.State, Random.ConsumptionCount);

        public OriginalAiHirePlacementResult Select(
            int mode,
            int offerSlot = 0) =>
            OriginalAiHirePlacementRules.Select(
                new PlayerId(0), mode, offerSlot,
                Owners, Gangs, Random);
    }
}
