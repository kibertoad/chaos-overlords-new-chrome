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

        Assert.Equal(26, OriginalAiScenarioStandingRules.Score(match, match.Players[0]));
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
                support: 20),
            new(setups[1], secondCash,
                [new MatchGangState(new GangId(20), setups[1].Id, 0, 63, 10)],
                support: 10)
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 3, 13),
                new MatchSiteState(2, 5, 15)
            ], owner: id is 0 or 1 ? setups[0].Id : id == 63 ? setups[1].Id : null))
            .ToArray();
        return new MatchState(data, new MatchSetup(
            scenario, GameDuration.SixMonths, 41, setups, AiDifficulty.Criminal),
            players, sectors);
    }
}
