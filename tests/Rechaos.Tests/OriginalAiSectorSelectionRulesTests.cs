using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiSectorSelectionRulesTests
{
    [Theory]
    [InlineData(1, -1, true)]
    [InlineData(2, 0, false)]
    [InlineData(3, 1, false)]
    [InlineData(4, 4, false)]
    public void ModesOneThroughFourSelectTheirExactOwnerPredicate(
        int mode,
        int owner,
        bool soloControl)
    {
        var facts = new Facts(source: 27, player: new PlayerId(mode == 4 ? 1 : 0));
        facts.Owners[28] = owner;
        if (soloControl) facts.SoloControl.Add(28);
        facts.PlayerOrder = [4, 1, 0, 2, 3, 5];

        Assert.Equal(28, facts.Select(mode, family: 2));
    }

    [Fact]
    public void ModeFiveUsesNeutralOwnedEnemyWeightsFiveTwoOne()
    {
        var facts = new Facts(source: 27);
        facts.Owners[28] = -1;
        facts.SoloControl.Add(28);
        facts.Owners[26] = 0;
        facts.Owners[19] = 1;

        Assert.Equal(28, facts.Select(mode: 5, family: 2));
    }

    [Fact]
    public void ModeFiveExcludesOwnedSectorWithPreviousChaos()
    {
        var facts = new Facts(source: 27);
        facts.Owners[28] = 0;
        facts.PriorChaos.Add(28);
        facts.Owners[26] = 1;

        Assert.Equal(26, facts.Select(mode: 5, family: 2));
    }

    [Fact]
    public void HostileHumanMultiplierAppliesAfterBaseWeight()
    {
        var facts = new Facts(source: 27);
        facts.Owners[28] = 0;
        facts.Owners[26] = 1;
        facts.HostileOwners.Add(1);
        facts.HumanOwners.Add(1);

        Assert.Equal(26, facts.Select(mode: 5, family: 2));
    }

    [Fact]
    public void SearchUsesFullClippedSquaresAndStopsAtFirstContributingRadius()
    {
        var facts = new Facts(source: 0);
        facts.Owners[1] = -1;
        facts.Owners[16] = -1;
        facts.Owners[3] = -1;
        facts.SoloControl.Add(16);
        facts.SoloControl.Add(3);
        var sectorOneQueries = 0;

        var result = facts.Select(mode: 1, family: 2, canSoloControl: sector =>
        {
            if (sector == 1) sectorOneQueries++;
            return facts.SoloControl.Contains(sector);
        });

        Assert.Equal(8, result);
        Assert.Equal(2, sectorOneQueries);
    }

    [Fact]
    public void CurrentSectorIsZeroedAfterScoring()
    {
        var facts = new Facts(source: 27);
        facts.Owners[28] = 0;

        Assert.Equal(28, facts.Select(mode: 2, family: 2));
    }

    [Fact]
    public void DisabledLateFilterDoesNotResumeRadiusSearch()
    {
        var facts = new Facts(source: 27);
        facts.Owners[28] = 1;
        facts.Disabled[28] = true;
        facts.Owners[29] = 1;

        Assert.Throws<InvalidOperationException>(() => facts.Select(mode: 3, family: 2));
    }

    [Fact]
    public void FamilyZeroAndOneApplyLateSoloControlFilterToNonOwnedSector()
    {
        foreach (var family in new[] { 0, 1 })
        {
            var facts = new Facts(source: 27);
            facts.Owners[28] = 1;

            Assert.Throws<InvalidOperationException>(() => facts.Select(mode: 3, family));
        }

        var unfiltered = new Facts(source: 27);
        unfiltered.Owners[28] = 1;
        Assert.Equal(28, unfiltered.Select(mode: 3, family: 2));
    }

    [Fact]
    public void EqualMaximumsKeepAscendingSectorOrderAndConsumeOneBoundedDraw()
    {
        var facts = new Facts(source: 27, seed: 4321);
        facts.Owners[18] = 1;
        facts.Owners[19] = 2;
        var expectedRandom = new DeterministicRandom(
            facts.Random.State, facts.Random.ConsumptionCount);
        var expected = new[] { 18, 19 }[expectedRandom.NextInclusive(2) - 1];

        var selected = facts.Select(mode: 3, family: 2);

        Assert.Equal(expected, selected);
        Assert.Equal(expectedRandom.State, facts.Random.State);
        Assert.Equal(expectedRandom.ConsumptionCount, facts.Random.ConsumptionCount);
        Assert.Equal(3, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void UniqueMaximumConsumesNoRandomness()
    {
        var facts = new Facts(source: 27, seed: 4321);
        facts.Owners[28] = 1;
        var stateBefore = facts.Random.State;

        Assert.Equal(28, facts.Select(mode: 3, family: 2));
        Assert.Equal(stateBefore, facts.Random.State);
        Assert.Equal(0, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void DistantRoutingAppliesHorizontalThenVerticalAndCanMoveDiagonally()
    {
        var facts = new Facts(source: 27);
        facts.Owners[45] = 1;

        Assert.Equal(36, facts.Select(mode: 3, family: 2));
    }

    [Fact]
    public void RoutingTestsVerticalPathFromTheAcceptedHorizontalStep()
    {
        var facts = new Facts(source: 27);
        facts.Owners[45] = 1;
        facts.Paths[36] = 6;

        Assert.Equal(28, facts.Select(mode: 3, family: 2));
    }

    [Fact]
    public void RoutingTestsVerticalPathFromSourceWhenHorizontalStepIsBlocked()
    {
        var facts = new Facts(source: 27);
        facts.Owners[45] = 1;
        facts.Paths[28] = 6;

        Assert.Equal(35, facts.Select(mode: 3, family: 2));
    }

    [Fact]
    public void LiteralPlayerOrderPredicatePreservesValueSearchAndMissingValueBehavior()
    {
        int[] values = [2, 0, 0, 1, 4, 4];

        Assert.True(OriginalAiSectorSelectionRules.LiteralPlayerOrderAccepts(0, 2, values));
        Assert.False(OriginalAiSectorSelectionRules.LiteralPlayerOrderAccepts(0, 1, values));
        Assert.False(OriginalAiSectorSelectionRules.LiteralPlayerOrderAccepts(0, 0, values));
        Assert.False(OriginalAiSectorSelectionRules.LiteralPlayerOrderAccepts(0, -1, values));
        Assert.True(OriginalAiSectorSelectionRules.LiteralPlayerOrderAccepts(5, 4, values));
        Assert.True(OriginalAiSectorSelectionRules.LiteralPlayerOrderAccepts(2, 5, values));
    }

    [Fact]
    public void NoPositiveCandidateThrowsInsteadOfInventingOriginalSentinel()
    {
        var facts = new Facts(source: 27);

        Assert.Throws<InvalidOperationException>(() => facts.Select(mode: 3, family: 2));
    }

    [Fact]
    public void MalformedShapesAndSelectorValuesAreRejected()
    {
        var facts = new Facts(source: 27);
        facts.Owners[28] = 1;

        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 0, family: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 6, family: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 3, family: 8));
        Assert.Throws<ArgumentException>(() => OriginalAiSectorSelectionRules.Select(
            3, 27, new PlayerId(0), 2,
            facts.Owners[..^1], facts.Disabled, facts.Paths,
            _ => false, _ => false, _ => false, _ => false,
            facts.PlayerOrder, facts.Random));
        facts.Owners[28] = 6;
        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 3, family: 2));
    }

    private sealed class Facts
    {
        public Facts(int source, PlayerId? player = null, int seed = 1)
        {
            Source = source;
            Player = player ?? new PlayerId(0);
            Owners = Enumerable.Repeat(-3, MatchLimits.SectorCount).ToArray();
            Owners[source] = Player.Value;
            Disabled = new bool[MatchLimits.SectorCount];
            Paths = new int[MatchLimits.SectorCount];
            PlayerOrder = [0, 1, 2, 3, 4, 5];
            Random = new DeterministicRandom(seed);
        }

        public int Source { get; }
        public PlayerId Player { get; }
        public int[] Owners { get; }
        public bool[] Disabled { get; }
        public int[] Paths { get; }
        public int[] PlayerOrder { get; set; }
        public HashSet<int> SoloControl { get; } = [];
        public HashSet<int> PriorChaos { get; } = [];
        public HashSet<int> HostileOwners { get; } = [];
        public HashSet<int> HumanOwners { get; } = [];
        public DeterministicRandom Random { get; }

        public int Select(
            int mode,
            int family,
            Func<int, bool>? canSoloControl = null) =>
            OriginalAiSectorSelectionRules.Select(
                mode, Source, Player, family,
                Owners, Disabled, Paths,
                canSoloControl ?? SoloControl.Contains,
                PriorChaos.Contains,
                HostileOwners.Contains,
                HumanOwners.Contains,
                PlayerOrder,
                Random);
    }
}
