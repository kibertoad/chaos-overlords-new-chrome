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
    public void ObjectiveScenarioUsesRecoveredScoreAndCompetitionOrder()
    {
        var match = CreateMatch(ScenarioId.Big40, 100, 200);
        match.Sectors[0].Owner = new PlayerId(0);
        match.Sectors[1].Owner = new PlayerId(0);
        match.Sectors[2].Owner = new PlayerId(1);

        var standings = EndgameRankingEvaluator.Evaluate(match);

        Assert.Equal(
        [
            new MatchStanding(new PlayerId(0), 1, 2),
            new MatchStanding(new PlayerId(1), 2, 1)
        ], standings);
    }

    [Fact]
    public void EliminatedPlayersFollowActiveStandingsWithoutAPlace()
    {
        var match = CreateMatch(ScenarioId.Greed, 100, 300, 200);
        match.Players[1].Status = PlayerStatus.Eliminated;

        var standings = EndgameRankingEvaluator.Evaluate(match);

        Assert.Equal(
        [
            new MatchStanding(new PlayerId(2), 1, 200),
            new MatchStanding(new PlayerId(0), 2, 100),
            new MatchStanding(new PlayerId(1), 0, -32_000)
        ], standings);
    }

    [Fact]
    public void OriginalInactiveSentinelStillContributesToActiveCompetitionPlace()
    {
        var match = CreateMatch(ScenarioId.Greed, -40_000, 0);
        match.Players[1].Status = PlayerStatus.Eliminated;

        var standings = EndgameRankingEvaluator.Evaluate(match);

        Assert.Equal(
        [
            new MatchStanding(new PlayerId(0), 6, -40_000),
            new MatchStanding(new PlayerId(1), 0, -32_000)
        ], standings);
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
