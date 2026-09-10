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
    public void ModeTenSwitchesBetweenHumanAndAnyOpposingOwnership()
    {
        var withHumans = new Facts(source: 27);
        withHumans.Owners[28] = 1;
        withHumans.Owners[26] = 2;
        withHumans.HumanOwners.Add(2);
        Assert.Equal(26, withHumans.Select(mode: 10, family: 11, hasHumanPlayers: true));

        var withoutHumans = new Facts(source: 27);
        withoutHumans.Owners[28] = 1;
        Assert.Equal(28, withoutHumans.Select(mode: 10, family: 11, hasHumanPlayers: false));
    }

    [Fact]
    public void ModeSixteenRoutesTowardFormationLeaderSector()
    {
        var facts = new Facts(source: 27);

        Assert.Equal(20, facts.Select(
            mode: 16, family: 11, formationSectorId: 4));
    }

    [Fact]
    public void ModeEightSumsPositiveUnfinishedSiteCashInOwnedSectors()
    {
        var facts = new Facts(source: 27);
        facts.Owners[28] = facts.Player.Value;
        facts.Owners[26] = facts.Player.Value;
        facts.SiteScores[28] = 7;
        facts.SiteScores[26] = 6;

        Assert.Equal(28, facts.Select(mode: 8, family: 3));
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
    public void DisabledLateFilterUsesZeroMaximumFallbackWithoutResumingRadiusSearch()
    {
        var facts = new Facts(source: 27);
        facts.Owners[28] = 1;
        facts.Disabled[28] = true;
        facts.Owners[29] = 1;
        var expectedRandom = new DeterministicRandom(
            facts.Random.State, facts.Random.ConsumptionCount);
        var expected = ZeroMaximumStep(facts.Source, facts.GangCounts, expectedRandom);

        Assert.Equal(expected, facts.Select(mode: 3, family: 2));
        Assert.Equal(expectedRandom.State, facts.Random.State);
        Assert.Equal(expectedRandom.ConsumptionCount, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void FamilyZeroAndOneApplyLateSoloControlFilterToNonOwnedSector()
    {
        foreach (var family in new[] { 0, 1 })
        {
            var facts = new Facts(source: 27);
            facts.Owners[28] = 1;
            var expectedRandom = new DeterministicRandom(
                facts.Random.State, facts.Random.ConsumptionCount);
            var expected = ZeroMaximumStep(facts.Source, facts.GangCounts, expectedRandom);

            Assert.Equal(expected, facts.Select(mode: 3, family));
            Assert.Equal(expectedRandom.State, facts.Random.State);
            Assert.Equal(expectedRandom.ConsumptionCount, facts.Random.ConsumptionCount);
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
    public void RoutingTestsVerticalCapacityFromTheAcceptedHorizontalStep()
    {
        var facts = new Facts(source: 27);
        facts.Owners[45] = 1;
        facts.GangCounts[36] = MatchLimits.FriendlyGangsPerSector;

        Assert.Equal(28, facts.Select(mode: 3, family: 2));
    }

    [Fact]
    public void RoutingTestsVerticalCapacityFromSourceWhenHorizontalStepIsBlocked()
    {
        var facts = new Facts(source: 27);
        facts.Owners[45] = 1;
        facts.GangCounts[28] = MatchLimits.FriendlyGangsPerSector;

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
    public void ZeroMaximumDrawsAmongAllSectorsAndCapacityRoutesOneStep()
    {
        const int source = 27;
        var facts = new Facts(source, seed: 4321);
        var expectedRandom = new DeterministicRandom(
            facts.Random.State, facts.Random.ConsumptionCount);
        var expected = ZeroMaximumStep(source, facts.GangCounts, expectedRandom);

        var selected = facts.Select(mode: 3, family: 2);

        Assert.Equal(expected, selected);
        Assert.Equal(expectedRandom.State, facts.Random.State);
        Assert.Equal(expectedRandom.ConsumptionCount, facts.Random.ConsumptionCount);

        var blocked = new Facts(source, seed: 4321);
        Array.Fill(blocked.GangCounts, MatchLimits.FriendlyGangsPerSector);
        Assert.Equal(source, blocked.Select(mode: 3, family: 2));
        Assert.Equal(3, blocked.Random.ConsumptionCount);
    }

    [Fact]
    public void ModesTwelveAndFourteenUseBigManObjectivesAndOwnedExclusion()
    {
        var excludingOwned = new Facts(source: 19);
        excludingOwned.Owners[27] = excludingOwned.Player.Value;
        excludingOwned.Owners[28] = -1;
        excludingOwned.Owners[18] = 1;
        var retainingOwned = new Facts(source: 19);
        retainingOwned.Owners[27] = retainingOwned.Player.Value;
        retainingOwned.Owners[28] = -1;
        retainingOwned.GangCounts[28] = MatchLimits.FriendlyGangsPerSector;
        retainingOwned.Owners[18] = 1;

        Assert.Equal(28, excludingOwned.Select(mode: 12, family: 13));
        Assert.Equal(27, retainingOwned.Select(mode: 14, family: 14));
    }

    [Fact]
    public void ModesThirteenAndFifteenUseHeadquartersAndOwnedExclusion()
    {
        var excludingOwned = new Facts(source: 2);
        excludingOwned.Owners[9] = excludingOwned.Player.Value;
        excludingOwned.Owners[12] = -1;
        excludingOwned.Owners[3] = 1;
        var retainingOwned = new Facts(source: 2);
        retainingOwned.Owners[9] = retainingOwned.Player.Value;
        retainingOwned.Owners[12] = -1;
        retainingOwned.Owners[3] = 1;

        Assert.Equal(11, excludingOwned.Select(mode: 13, family: 13));
        Assert.Equal(9, retainingOwned.Select(mode: 15, family: 14));
    }

    [Fact]
    public void ObjectiveModesRejectSectorsAtSixFriendlyGangs()
    {
        var facts = new Facts(source: 19);
        facts.Owners[27] = -1;
        facts.Owners[28] = -1;
        facts.GangCounts[27] = MatchLimits.FriendlyGangsPerSector;

        Assert.Equal(28, facts.Select(mode: 12, family: 13));
    }

    [Fact]
    public void ObjectiveModesCompoundTheirHostileHumanWeight()
    {
        var facts = new Facts(source: 20);
        facts.Owners[27] = 2;
        facts.Owners[28] = 1;
        facts.HostileOwners.Add(1);
        facts.HumanOwners.Add(1);

        Assert.Equal(5, OriginalAiSectorSelectionRules.ObjectiveModeBaseScore(
            1, facts.HostileOwners.Contains, facts.HumanOwners.Contains));
        Assert.Equal(1, OriginalAiSectorSelectionRules.ObjectiveModeBaseScore(
            2, facts.HostileOwners.Contains, facts.HumanOwners.Contains));
        Assert.Equal(28, facts.Select(mode: 14, family: 14));
        Assert.Equal(0, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void ModeSevenScoresSupportAndExcludesPriorInfluenceSectors()
    {
        var facts = new Facts(source: 20);
        facts.Owners[21] = facts.Player.Value;
        facts.SiteScores[21] = 3;
        facts.Owners[28] = facts.Player.Value;
        facts.SiteScores[28] = 9;
        facts.PriorInfluence.Add(28);

        Assert.Equal(21, facts.Select(mode: 7, family: 5));
        Assert.Equal(0, facts.Random.ConsumptionCount);
    }

    [Fact]
    public void MalformedShapesAndSelectorValuesAreRejected()
    {
        var facts = new Facts(source: 27);
        facts.Owners[28] = 1;

        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 0, family: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 6, family: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 3, family: 8));
        Assert.Throws<ArgumentNullException>(() => OriginalAiSectorSelectionRules.Select(
            8, 27, new PlayerId(0), 3,
            facts.Owners, facts.Disabled, facts.GangCounts,
            _ => false, _ => false, _ => false, _ => false,
            facts.PlayerOrder, facts.Random));
        Assert.Throws<ArgumentNullException>(() => OriginalAiSectorSelectionRules.Select(
            7, 27, new PlayerId(0), 5,
            facts.Owners, facts.Disabled, facts.GangCounts,
            _ => false, _ => false, _ => false, _ => false,
            facts.PlayerOrder, facts.Random,
            unfinishedSiteScore: facts.SiteScores.ElementAt));
        Assert.Throws<ArgumentException>(() => OriginalAiSectorSelectionRules.Select(
            3, 27, new PlayerId(0), 2,
            facts.Owners[..^1], facts.Disabled, facts.GangCounts,
            _ => false, _ => false, _ => false, _ => false,
            facts.PlayerOrder, facts.Random));
        facts.GangCounts[28] = -1;
        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 3, family: 2));
        facts.GangCounts[28] = AiPlanningState.GangSlotsPerPlayer + 1;
        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 3, family: 2));
        facts.GangCounts[28] = 0;
        facts.Owners[28] = 6;
        Assert.Throws<ArgumentOutOfRangeException>(() => facts.Select(mode: 3, family: 2));
    }

    private static int ZeroMaximumStep(
        int source,
        IReadOnlyList<int> gangCounts,
        DeterministicRandom random)
    {
        var target = random.NextInclusive(MatchLimits.SectorCount) - 1;
        var result = source;
        if (source % MatchLimits.BoardWidth < target % MatchLimits.BoardWidth
            && gangCounts[result + 1] < MatchLimits.FriendlyGangsPerSector)
            result++;
        if (source % MatchLimits.BoardWidth > target % MatchLimits.BoardWidth
            && gangCounts[result - 1] < MatchLimits.FriendlyGangsPerSector)
            result--;
        if (source / MatchLimits.BoardWidth < target / MatchLimits.BoardWidth
            && gangCounts[result + MatchLimits.BoardWidth]
                < MatchLimits.FriendlyGangsPerSector)
            result += MatchLimits.BoardWidth;
        if (source / MatchLimits.BoardWidth > target / MatchLimits.BoardWidth
            && gangCounts[result - MatchLimits.BoardWidth]
                < MatchLimits.FriendlyGangsPerSector)
            result -= MatchLimits.BoardWidth;
        return result;
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
            GangCounts = new int[MatchLimits.SectorCount];
            PlayerOrder = [0, 1, 2, 3, 4, 5];
            Random = new DeterministicRandom(seed);
        }

        public int Source { get; }
        public PlayerId Player { get; }
        public int[] Owners { get; }
        public bool[] Disabled { get; }
        public int[] GangCounts { get; }
        public int[] PlayerOrder { get; set; }
        public HashSet<int> SoloControl { get; } = [];
        public HashSet<int> PriorChaos { get; } = [];
        public HashSet<int> PriorInfluence { get; } = [];
        public HashSet<int> HostileOwners { get; } = [];
        public HashSet<int> HumanOwners { get; } = [];
        public int[] SiteScores { get; } = new int[MatchLimits.SectorCount];
        public DeterministicRandom Random { get; }

        public int Select(
            int mode,
            int family,
            Func<int, bool>? canSoloControl = null,
            bool? hasHumanPlayers = null,
            int? formationSectorId = null) =>
            OriginalAiSectorSelectionRules.Select(
                mode, Source, Player, family,
                Owners, Disabled, GangCounts,
                canSoloControl ?? SoloControl.Contains,
                PriorChaos.Contains,
                HostileOwners.Contains,
                HumanOwners.Contains,
                PlayerOrder,
                Random,
                hasHumanPlayers,
                formationSectorId,
                SiteScores.ElementAt,
                PriorInfluence.Contains);
    }
}
