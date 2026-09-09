using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class EndgameRankingTests
{
    [Fact]
    public void GreedRanksDescendingCashAndPreservesCompetitionTies()
    {
        var match = CreateMatch(ScenarioId.Greed, 100, 200, 200, 50);

        var standings = EndgameRankingEvaluator.EvaluateTimed(match);

        Assert.Equal(
        [
            new MatchStanding(new PlayerId(1), 1, 200),
            new MatchStanding(new PlayerId(2), 1, 200),
            new MatchStanding(new PlayerId(0), 3, 100),
            new MatchStanding(new PlayerId(3), 4, 50)
        ], standings);
    }

    [Fact]
    public void ObjectiveScenarioRejectsInventedRankingScore()
    {
        var match = CreateMatch(ScenarioId.Big40, 100, 200);

        var error = Assert.Throws<ArgumentException>(() => EndgameRankingEvaluator.EvaluateTimed(match));

        Assert.Contains("timed", error.Message);
    }

    private static MatchState CreateMatch(ScenarioId scenario, params int[] cash)
    {
        var data = BundledOriginalData.Load();
        var setups = cash.Select((_, id) =>
            new MatchPlayerSetup(new PlayerId(id), $"PLAYER {id + 1}", PlayerController.Human)).ToArray();
        var setup = new MatchSetup(scenario, GameDuration.SixMonths, 1996, setups);
        var players = setups.Select((player, id) => new MatchPlayerState(player, cash[id])).ToArray();
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ]))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
