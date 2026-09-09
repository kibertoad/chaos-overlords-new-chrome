using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalCityGeneratorTests
{
    [Fact]
    public void FixedSeedProducesStableBalancedCityAndRandomContinuation()
    {
        var data = BundledOriginalData.Load();
        var firstRandom = new DeterministicRandom(1996);
        var secondRandom = new DeterministicRandom(1996);

        var first = OriginalCityGenerator.Generate(data, ScenarioId.Greed, firstRandom);
        var second = OriginalCityGenerator.Generate(data, ScenarioId.Greed, secondRandom);

        Assert.Equal(MatchLimits.SectorCount, first.Length);
        Assert.Equal(first.Select(Snapshot), second.Select(Snapshot));
        Assert.Equal(firstRandom.State, secondRandom.State);
        Assert.Equal(firstRandom.ConsumptionCount, secondRandom.ConsumptionCount);
        Assert.All(first, sector =>
        {
            Assert.InRange(sector.Income, ManualRules.MinimumSectorIncome, ManualRules.MaximumSectorIncome);
            Assert.Equal(17 - sector.Income, sector.Tolerance);
            Assert.Equal(3, sector.Sites.Select(site => site.DefinitionId).Distinct().Count());
            AssertBalanced(data, sector);
        });
    }

    [Fact]
    public void ArmageddonNeverGeneratesResearchBuildings()
    {
        var city = OriginalCityGenerator.Generate(
            BundledOriginalData.Load(), ScenarioId.Armageddon, new DeterministicRandom(17));

        Assert.DoesNotContain(city.SelectMany(sector => sector.Sites), site => site.DefinitionId is 4 or 8);
    }

    [Fact]
    public void HeadquartersUseRecoveredCandidateSetAndStablePermutation()
    {
        var data = BundledOriginalData.Load();
        var firstRandom = new DeterministicRandom(42);
        var secondRandom = new DeterministicRandom(42);
        var first = OriginalCityGenerator.Generate(data, ScenarioId.Greed, firstRandom);
        var second = OriginalCityGenerator.Generate(data, ScenarioId.Greed, secondRandom);

        var firstAssignments = OriginalCityGenerator.AssignHeadquarters(first, firstRandom);
        var secondAssignments = OriginalCityGenerator.AssignHeadquarters(second, secondRandom);

        Assert.Equal(firstAssignments, secondAssignments);
        Assert.Equal(OriginalCityGenerator.HeadquartersCandidates.Order(), firstAssignments.Order());
        Assert.All(firstAssignments, sectorId =>
            Assert.Equal(MatchBootstrap.HeadquartersDefinitionId, first[sectorId].Sites[0].DefinitionId));
        Assert.Equal(firstRandom.State, secondRandom.State);
        Assert.Equal(firstRandom.ConsumptionCount, secondRandom.ConsumptionCount);
    }

    [Fact]
    public void FactoryCreatesRecoveredStartsAndPreservesConsumedRandomState()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players);

        var first = OriginalMatchFactory.Create(data, setup);
        var second = OriginalMatchFactory.Create(data, setup);

        Assert.Equal(MatchStateHasher.ComputeSha256(first), MatchStateHasher.ComputeSha256(second));
        Assert.True(first.Random.ConsumptionCount > 0);
        Assert.Equal(first.Random.State, second.Random.State);
        Assert.All(first.Players, player =>
        {
            Assert.Equal(OriginalMatchFactory.StandardStartingCash, player.Cash);
            Assert.Empty(player.HirePool);
            var rightHands = Assert.Single(player.Gangs);
            Assert.Equal(MatchBootstrap.RightHandsDefinitionId, rightHands.DefinitionId);
            Assert.Equal(ManualRules.MaximumForce, rightHands.Force);
            Assert.Contains(rightHands.SectorId, OriginalCityGenerator.HeadquartersCandidates);
            Assert.Equal(player.Id, first.Sectors[rightHands.SectorId].Owner);
        });
    }

    [Fact]
    public void FirstHireInteractionFillsThreeDistinctOriginalRangeOffers()
    {
        var data = BundledOriginalData.Load();
        var player = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        var match = OriginalMatchFactory.Create(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [player]));
        var before = match.Random.ConsumptionCount;

        match.FinishUpkeep();
        match.FinishCommand(player.Id);
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();

        Assert.Equal(TurnPhase.Hire, match.Coordinator.Phase);
        Assert.Equal(MatchLimits.HireOffersPerPlayer, match.Players[0].HirePool.Count);
        Assert.Equal(match.Players[0].HirePool.Count, match.Players[0].HirePool.Distinct().Count());
        Assert.All(match.Players[0].HirePool, offer => Assert.InRange(offer, (short)1, (short)89));
        Assert.True(match.Random.ConsumptionCount >= before + 9);
    }

    private static string Snapshot(MatchSectorState sector) =>
        $"{sector.Id}:{sector.Income}:{sector.Tolerance}:{string.Join(',', sector.Sites.Select(site => site.DefinitionId))}";

    private static void AssertBalanced(OriginalData data, MatchSectorState sector)
    {
        var definitions = sector.Sites.Select(site => data.Sites.Single(value => value.Id == site.DefinitionId));
        var sums = new int[14];
        foreach (var definition in definitions)
        {
            var stats = definition.Stats;
            short[] values =
            [
                stats.Combat, stats.Defense, stats.Stealth, stats.Detect,
                stats.Chaos, stats.Control, stats.Heal, stats.Influence,
                stats.Research, stats.Strength, stats.Blade, stats.Range,
                stats.Fighting, stats.MartialArts
            ];
            for (var index = 0; index < sums.Length; index++) sums[index] += values[index];
        }
        Assert.All(sums, sum => Assert.InRange(sum, -6, 6));
    }
}
