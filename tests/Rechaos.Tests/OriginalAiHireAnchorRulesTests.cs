using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiHireAnchorRulesTests
{
    [Fact]
    public void Selector24RequiresOwnedCenterAndCountsOnlyAvailableNeutralNeighbors()
    {
        var facts = new Facts();
        facts.Owners[27] = 0;
        facts.Owners[18] = -1;
        facts.Owners[19] = -1;
        facts.Availability[19] = 1;

        Assert.Equal(1, facts.CountVacancies(27));

        facts.Owners[27] = 1;
        Assert.Equal(0, facts.CountVacancies(27));
    }

    [Fact]
    public void LiteralNeighborhoodPreventsRowWrapButReadsIndex64Sentinel()
    {
        var facts = new Facts(defaultOwner: 0);
        facts.Owners[56] = 0;
        facts.Owners[55] = -1; // Would be a wrapped upper-row neighbor without the row guard.
        facts.Owners[64] = -1;

        Assert.Equal(1, facts.CountVacancies(56));
        Assert.Equal(1, OriginalAiHireAnchorRules.CountNonOwnedNeighbors(
            new PlayerId(0), 56, facts.Owners));
    }

    [Fact]
    public void NormalFirstPassUsesLargestPositiveVacancyCountAndFirstStrictTie()
    {
        var facts = new Facts(defaultOwner: 1);
        facts.Owners[5] = 0;
        facts.Owners[7] = 0;
        facts.Owners[10] = 0;
        facts.Owners[4] = -1;
        facts.Owners[6] = -1;
        facts.Owners[9] = -1;
        facts.Owners[11] = -1;
        facts.Occupancy[10] = 6;

        Assert.Equal(5, facts.Select(ScenarioId.Power));
    }

    [Fact]
    public void NormalFirstPassReinvokesSelector24OnlyWhenStoringNewMaximum()
    {
        var owners = Enumerable.Repeat(1, 65).ToArray();
        owners[10] = 0;
        owners[9] = -1;
        var availability = new CountingList<byte>(new byte[65]);

        var selected = OriginalAiHireAnchorRules.Select(
            new PlayerId(0), ScenarioId.Power, 27, owners, availability,
            _ => 0,
            _ => 1);

        Assert.Equal(10, selected);
        Assert.Equal(2, availability.ReadsAt(9));
    }

    [Fact]
    public void NormalSecondPassUsesFirstEligibleSectorWithoutPriorChaos()
    {
        var facts = new Facts(defaultOwner: 1);
        facts.Owners[12] = 0;
        facts.Owners[20] = 0;
        facts.PriorChaos[12] = 1;

        Assert.Equal(20, facts.Select(ScenarioId.Power));
    }

    [Fact]
    public void NormalThirdPassMinimizesNonzeroNonOwnedNeighborsWithFirstTies()
    {
        var facts = new Facts(defaultOwner: 0);
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
            facts.Occupancy[sectorId] = 6;
        facts.Occupancy[10] = 0;
        facts.Occupancy[20] = 0;
        facts.PriorChaos[10] = 1;
        facts.PriorChaos[20] = 1;
        facts.Owners[9] = 1;
        facts.Owners[19] = 1;
        facts.Owners[21] = 1;

        Assert.Equal(10, facts.Select(ScenarioId.Power));
    }

    [Fact]
    public void NormalSelectionReturnsMinusOneWhenEveryOwnedSectorIsFull()
    {
        var facts = new Facts(defaultOwner: 0);
        Array.Fill(facts.Occupancy, 6);

        Assert.Equal(-1, facts.Select(ScenarioId.Power));
    }

    [Fact]
    public void BigManPreservesDuplicateSectorScansAcrossItsTwoRadii()
    {
        var facts = new Facts(defaultOwner: 1);
        facts.Owners[18] = 0;
        facts.Owners[34] = 0;
        facts.Occupancy[18] = 6;
        var queried = new List<int>();

        var result = facts.Select(
            ScenarioId.BigMan,
            activeGangCount: sectorId =>
            {
                queried.Add(sectorId);
                return facts.Occupancy[sectorId];
            });

        Assert.Equal(34, result);
        Assert.Equal([18, 18, 34], queried);
    }

    [Fact]
    public void BigManFallsBackToAnchorEvenWhenItIsFull()
    {
        var facts = new Facts(defaultOwner: 1, anchor: 42);
        facts.Owners[42] = 0;
        facts.Occupancy[42] = 6;

        Assert.Equal(42, facts.Select(ScenarioId.BigMan));
    }

    [Fact]
    public void Selector26CountsUnavailableAndNeutralNonOwnedNeighbors()
    {
        var facts = new Facts(defaultOwner: 0);
        facts.Owners[27] = 0;
        facts.Owners[18] = -1;
        facts.Owners[19] = 1;
        facts.Availability[18] = 1;

        Assert.Equal(2, OriginalAiHireAnchorRules.CountNonOwnedNeighbors(
            new PlayerId(0), 27, facts.Owners));
    }

    private sealed class Facts
    {
        public Facts(int defaultOwner = 1, int anchor = 27)
        {
            Array.Fill(Owners, defaultOwner);
            Anchor = anchor;
        }

        public int Anchor { get; }
        public int[] Owners { get; } = new int[65];
        public byte[] Availability { get; } = new byte[65];
        public int[] Occupancy { get; } = new int[MatchLimits.SectorCount];
        public int[] PriorChaos { get; } = new int[MatchLimits.SectorCount];

        public int CountVacancies(int center) =>
            OriginalAiHireAnchorRules.CountAvailableNeutralNeighbors(
                new PlayerId(0), center, Owners, Availability);

        public int Select(
            ScenarioId scenario,
            Func<int, int>? activeGangCount = null) =>
            OriginalAiHireAnchorRules.Select(
                new PlayerId(0), scenario, Anchor, Owners, Availability,
                activeGangCount ?? (sectorId => Occupancy[sectorId]),
                sectorId => PriorChaos[sectorId]);
    }

    private sealed class CountingList<T>(IReadOnlyList<T> values) : IReadOnlyList<T>
    {
        private readonly int[] _reads = new int[values.Count];

        public int Count => values.Count;

        public T this[int index]
        {
            get
            {
                _reads[index]++;
                return values[index];
            }
        }

        public int ReadsAt(int index) => _reads[index];

        public IEnumerator<T> GetEnumerator() => values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}
