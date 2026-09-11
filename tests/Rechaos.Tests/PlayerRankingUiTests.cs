using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class PlayerRankingUiTests
{
    [Fact]
    public void PortraitsFollowSixOriginalColorRailsAndStandingHeights()
    {
        Assert.Equal(new Rectangle(104, 125, 344, 209), PlayerRankingLayout.Panel);
        Assert.Equal(new Rectangle(200, 143, 32, 32), PlayerRankingLayout.Portrait(0, 0));
        Assert.Equal(new Rectangle(402, 283, 32, 32), PlayerRankingLayout.Portrait(5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerRankingLayout.Portrait(6, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerRankingLayout.Portrait(0, 6));
    }

    [Fact]
    public void TimedRankingUsesCompetitionTiesOnCanonicalScenarioScore()
    {
        var state = CreateMatch(ScenarioId.Greed, [100, 200, 200, 50]);

        Assert.Equal(
        [
            new PlayerRankingEntry(new PlayerId(0), 2, 100),
            new PlayerRankingEntry(new PlayerId(1), 0, 200),
            new PlayerRankingEntry(new PlayerId(2), 0, 200),
            new PlayerRankingEntry(new PlayerId(3), 3, 50)
        ], PlayerRankingPresentation.Project(state));
    }

    [Fact]
    public void ObjectiveRankingUsesProgressAndOmitsEliminatedPlayers()
    {
        var state = CreateMatch(
            ScenarioId.Big40,
            [0, 0, 0],
            sectorOwners: [0, 0, 1],
            eliminatedPlayer: 2);

        Assert.Equal(
        [
            new PlayerRankingEntry(new PlayerId(0), 0, 2),
            new PlayerRankingEntry(new PlayerId(1), 1, 1)
        ], PlayerRankingPresentation.Project(state));
    }

    [Fact]
    public void BigManRankingUsesCurrentCenterControlRatherThanAccumulatedVictoryPoints()
    {
        var state = CreateMatch(ScenarioId.BigMan, [0, 0]);
        state.Players[0].BigManPoints = 39;
        state.Sectors[27].Owner = new PlayerId(1);

        Assert.Equal(
        [
            new PlayerRankingEntry(new PlayerId(0), 1, 0),
            new PlayerRankingEntry(new PlayerId(1), 0, 1)
        ], PlayerRankingPresentation.Project(state));
    }

    private static MatchState CreateMatch(
        ScenarioId scenario,
        int[] cash,
        int[]? sectorOwners = null,
        int? eliminatedPlayer = null)
    {
        var data = BundledOriginalData.Load();
        var setups = cash.Select((_, id) => new MatchPlayerSetup(
            new PlayerId(id), $"PLAYER {id + 1}", PlayerController.Human)).ToArray();
        var setup = new MatchSetup(scenario, GameDuration.SixMonths, 1996, setups);
        var players = setups.Select((player, id) => new MatchPlayerState(
            player,
            cash[id],
            status: id == eliminatedPlayer ? PlayerStatus.Eliminated : PlayerStatus.Active)).ToArray();
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], id < (sectorOwners?.Length ?? 0)
                ? new PlayerId(sectorOwners![id])
                : null))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
