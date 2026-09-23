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
        Assert.Equal(new Rectangle(104, 124, 344, 209), PlayerRankingLayout.Panel);
        Assert.Equal(new Rectangle(202, 142, 32, 32), PlayerRankingLayout.Portrait(0, 0));
        Assert.Equal(new Rectangle(402, 282, 32, 32), PlayerRankingLayout.Portrait(5, 5));
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
    public void BigManRankingUsesAccumulatedVictoryPointsRatherThanCurrentCenterControl()
    {
        var state = CreateMatch(ScenarioId.BigMan, [0, 0]);
        state.Players[0].BigManPoints = 39;
        state.Sectors[27].Owner = new PlayerId(1);

        Assert.Equal(
        [
            new PlayerRankingEntry(new PlayerId(0), 0, 39),
            new PlayerRankingEntry(new PlayerId(1), 1, 0)
        ], PlayerRankingPresentation.Project(state));
    }

    [Fact]
    public void TooltipExplainsBasisScoreAndEveryStanding()
    {
        var state = CreateMatch(ScenarioId.Greed, [100, 2_500, 2_500, 50]);
        var entries = PlayerRankingPresentation.Project(state);

        var lines = PlayerRankingTooltip.Lines(state, entries[1], entries);

        Assert.Equal(
        [
            "PLAYER 2",
            "PLACE 1 OF 4 (TIED)",
            "GREED RATES: CASH ON HAND",
            "SCORE: 2,500",
            "  CASH: $2,500",
            "",
            "ALL SCORES:",
            "> 1. PLAYER 2              2,500",
            "  1. PLAYER 3              2,500",
            "  3. PLAYER 1                100",
            "  4. PLAYER 4                 50"
        ], lines);
    }

    [Fact]
    public void DominanceTooltipShowsWeightedComponentsBehindTheScore()
    {
        var state = CreateMatch(ScenarioId.Dominance, [400, 0], sectorOwners: [0, 0]);
        state.Players[0].Support = 3;
        var entries = PlayerRankingPresentation.Project(state);

        var lines = PlayerRankingTooltip.Lines(state, entries[0], entries);

        Assert.Equal("SCORE: 49", lines[3]);
        Assert.Equal("  CASH          $400 X 1    = 400", lines[4]);
        Assert.Equal("  SUPPORT          3 X 10   = 30", lines[5]);
        Assert.Equal("  SECTORS          2 X 30   = 60", lines[6]);
        Assert.Equal("  TOTAL / 10 = SCORE", lines[7]);
    }

    [Fact]
    public void TooltipAppearsOnlyOverAPortrait()
    {
        var state = CreateMatch(ScenarioId.Power, [0, 0], sectorOwners: [1]);
        var portrait = PlayerRankingLayout.Portrait(1, 0);

        Assert.Equal("PLAYER 2", PlayerRankingTooltip.At(portrait.Center, state)[0]);
        Assert.Empty(PlayerRankingTooltip.At(new Point(0, 0), state));
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
