using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Microsoft.Xna.Framework;
using Xunit;

namespace Rechaos.Tests;

public sealed class EndgameNoticePresentationTests
{
    // RULE-AWARDS-002: the splash goes to the lone active player.
    [Fact]
    public void LoneActiveHumanGetsTheSplashWithConfiguredPortrait()
    {
        var state = CompletedEliminationMatch(humanPortrait: 9, eliminated: 1);

        var notice = EndgameNoticePresentation.Survivor(state);

        Assert.NotNull(notice);
        Assert.Equal(new PlayerId(0), notice.Player);
        Assert.Equal(9, notice.PortraitId);
    }

    // RULE-AWARDS-002: a computer survivor's splash is shown to the humans (FND-AWARDS-004).
    [Fact]
    public void LoneActiveComputerGetsTheSplash()
    {
        var state = CompletedEliminationMatch(humanPortrait: 4, eliminated: 0);

        var notice = EndgameNoticePresentation.Survivor(state);

        Assert.NotNull(notice);
        Assert.Equal(new PlayerId(1), notice.Player);
        Assert.Equal(1, notice.PortraitId);
    }

    // RULE-AWARDS-002: with several players active, as at a time limit, the table comes first.
    [Fact]
    public void SeveralActivePlayersOpenOnTheTable()
    {
        var state = CompletedMatch(ScenarioId.Greed, humanPortrait: 2, humanCount: 2);

        Assert.Null(EndgameNoticePresentation.Survivor(state));
    }

    [Fact]
    public void EndgameRowsFollowAuthoritativeCompetitionStandings()
    {
        var state = CompletedMatch(ScenarioId.Greed, humanPortrait: 3);

        var rows = EndgamePresentation.Rows(state);

        Assert.Equal(state.Outcome!.Standings.Select(standing => standing.Player),
            rows.Select(row => row.Player));
        Assert.All(state.Outcome.Standings.Zip(rows), pair =>
        {
            Assert.Equal(pair.First.Place, pair.Second.Place);
            Assert.Equal(state.FindPlayer(pair.First.Player)!.Setup.Name, pair.Second.Label);
        });
    }

    [Fact]
    public void EndgameAtlasLayoutProvidesSixRowsFiveAwardsAndExactControls()
    {
        Assert.Equal(new Rectangle(106, 25, 428, 410), EndgameLayout.Panel);
        Assert.Equal(new Rectangle(428, 33, 48, 48), EndgameLayout.Awards);
        Assert.Equal(new Rectangle(480, 33, 48, 48), EndgameLayout.Stats);
        Assert.Equal(new Rectangle(428, 377, 100, 48), EndgameLayout.Done);
        Assert.Equal(new Rectangle(132, 360, 64, 64), EndgameLayout.Portrait(5));
        Assert.Equal(new Rectangle(113, 361, 16, 32), EndgameLayout.PlayerMarker(5));
        Assert.Equal(new Rectangle(32, 144, 16, 32),
            EndgameLayout.PlayerMarkerSource(new PlayerId(3), 3));
        Assert.Equal(new Rectangle(110, 30, 312, 393), EndgameNoticeLayout.Panel);
        Assert.Equal(new Rectangle(126, 54, 64, 64), EndgameNoticeLayout.Portrait);
        Assert.Equal(
            [new Rectangle(110, 30, 40, 12), new Rectangle(110, 42, 13, 79), new Rectangle(110, 121, 40, 302)],
            EndgameNoticeLayout.VictoryColourBands);
        Assert.Equal(158, EndgameNoticeLayout.NameCenterX);
        Assert.Equal(46, EndgameNoticeLayout.NameY);
        Assert.Equal(new Rectangle(96, 112, 160, 64), EndgameLayout.StatisticsSource);
        Assert.Equal(new Rectangle(262, 360, 160, 64), EndgameLayout.StatisticsDestination(5));
        Assert.Equal(new Rectangle(371, 367, 48, 7), EndgameLayout.StatisticValueField(5, 0));
        Assert.Equal(new Rectangle(383, 409, 36, 7), EndgameLayout.StatisticValueField(5, 4));
        Assert.Equal(new Rectangle(200, 0, 50, 48),
            EndgameLayout.AwardSource(EndgameAward.Safe));
        Assert.Throws<ArgumentOutOfRangeException>(() => EndgameLayout.Portrait(6));
    }

    private static MatchState CompletedMatch(
        ScenarioId scenario,
        short humanPortrait,
        int humanCount = 1)
    {
        var setups = Enumerable.Range(0, humanCount)
            .Select(index => new MatchPlayerSetup(new PlayerId(index), $"PLAYER {index + 1}",
                PlayerController.Human, index == 0 ? humanPortrait : checked((short)index)))
            .ToArray();
        var state = OriginalMatchFactory.Create(BundledOriginalData.Load(),
            new MatchSetup(scenario, GameDuration.SixMonths, 404, setups,
                allowSparsePlayerIds: humanCount < MatchLimits.PlayerCount));
        while (state.Outcome is null)
        {
            state.FinishUpkeep();
            foreach (var player in state.Players) state.FinishCommand(player.Id);
            foreach (var _ in TurnStructure.ExecutionOrder) state.FinishExecutionPhase();
            foreach (var player in state.Players) state.FinishHire(player.Id);
            state.FinishPlayerElimination();
        }
        Assert.NotNull(state.Outcome);
        return state;
    }

    private static MatchState CompletedEliminationMatch(short humanPortrait, int eliminated)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "PLAYER 1", PlayerController.Human, humanPortrait),
            new(new PlayerId(1), "PLAYER 2", PlayerController.Computer, 1)
        ];
        var setup = new MatchSetup(
            ScenarioId.Eliminate, GameDuration.SixMonths, 404, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], 20,
                [new MatchGangState(new GangId(10), new PlayerId(0), 0, 0, eliminated == 0 ? (short)0 : (short)10)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), new PlayerId(1), 0, 1, eliminated == 1 ? (short)0 : (short)10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 1 - eliminated ? new PlayerId(1 - eliminated) : null))
            .ToArray();
        var state = new MatchState(data, setup, players, sectors);
        state.FinishUpkeep();
        foreach (var player in state.Players) state.FinishCommand(player.Id);
        foreach (var _ in TurnStructure.ExecutionOrder) state.FinishExecutionPhase();
        foreach (var player in state.Players) state.FinishHire(player.Id);
        state.FinishPlayerElimination();
        Assert.NotNull(state.Outcome);
        return state;
    }
}
