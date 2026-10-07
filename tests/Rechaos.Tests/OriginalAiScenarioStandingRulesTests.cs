using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalAiScenarioStandingRulesTests
{
    [Theory]
    [InlineData(ScenarioId.Greed)]
    [InlineData(ScenarioId.Power)]
    [InlineData(ScenarioId.Acceptance)]
    [InlineData(ScenarioId.Dominance)]
    [InlineData(ScenarioId.Big40)]
    [InlineData(ScenarioId.Armageddon)]
    public void NumericScenariosUseCompetitionStandingAndInactiveSentinel(ScenarioId scenario)
    {
        var match = CreateMatch(scenario, firstCash: 200, secondCash: 100);

        var standings = OriginalAiScenarioStandingRules.Build(match);

        Assert.Equal(0, standings[0]);
        Assert.Equal(1, standings[1]);
        Assert.All(standings.Skip(2), standing =>
            Assert.Equal(OriginalAiScenarioStandingRules.InactiveStanding, standing));
    }

    [Fact]
    public void KillEmAllGivesEveryActivePlayerTheSameCurrentScore()
    {
        var match = CreateMatch(ScenarioId.KillEmAll, firstCash: 200, secondCash: 100);

        Assert.Equal([0, 0, 255, 255, 255, 255],
            OriginalAiScenarioStandingRules.Build(match));
    }

    [Fact]
    public void DominanceAppliesDurationWeightsBeforeIntegerDivision()
    {
        var match = CreateMatch(ScenarioId.Dominance, firstCash: 9, secondCash: 0);

        // (9 cash x 1 + 2 sectors x 30 + 4 Support x 10) / 10
        Assert.Equal(10, OriginalAiScenarioStandingRules.Score(match, match.Players[0]));
    }

    [Fact]
    public void BigManUsesAccumulatedPointsWithoutRescoringCurrentCenterControl()
    {
        var match = CreateMatch(ScenarioId.BigMan, firstCash: 0, secondCash: 0);
        match.Players[0].BigManPoints = 39;
        match.Players[1].BigManPoints = 3;
        match.Sectors[27].Owner = new PlayerId(1);
        match.Sectors[28].Owner = new PlayerId(1);

        Assert.Equal(39, OriginalAiScenarioStandingRules.Score(match, match.Players[0]));
        Assert.Equal(3, OriginalAiScenarioStandingRules.Score(match, match.Players[1]));
        Assert.Equal([0, 1, 255, 255, 255, 255],
            OriginalAiScenarioStandingRules.Build(match));
    }

    private static MatchState CreateMatch(
        ScenarioId scenario,
        int firstCash,
        int secondCash)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "FIRST", PlayerController.Computer),
            new(new PlayerId(1), "SECOND", PlayerController.Computer)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], firstCash,
                [new MatchGangState(new GangId(10), setups[0].Id, 0, 0, 10)],
                support: 4),
            new(setups[1], secondCash,
                [new MatchGangState(new GangId(20), setups[1].Id, 0, 63, 10)],
                support: 2)
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                // RULE-OBJECTIVE-002: each owned sector's site of definition 3 (Support 2) is
                // complete, so the first player scores 4 Support and the second 2.
                id is 0 or 1 or 63
                    ? new MatchSiteState(1, 3, 0, id == 63 ? setups[1].Id : setups[0].Id)
                    : new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 5, 15)
            ], owner: id is 0 or 1 ? setups[0].Id : id == 63 ? setups[1].Id : null))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, 41, setups, MatchDeviations.Original, AiDifficulty.Criminal),
            players, sectors);
    }
}
