using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Microsoft.Xna.Framework;
using Xunit;

namespace Rechaos.Tests;

public sealed class EndgameNoticePresentationTests
{
    [Fact]
    public void SingleHumanWinnerGetsVictoryNoticeWithConfiguredPortrait()
    {
        var state = CompletedMatch(ScenarioId.Greed, humanPortrait: 9);

        var notice = EndgameNoticePresentation.For(state);

        Assert.NotNull(notice);
        Assert.Equal(EndgameNoticeKind.Victory, notice.Kind);
        Assert.Equal(new PlayerId(0), notice.Player);
        Assert.Equal(9, notice.PortraitId);
        Assert.True(EndgameNoticePresentation.ContinuesToSummary(notice.Kind));
    }

    [Fact]
    public void SingleHumanLoserGetsEliminationNotice()
    {
        var state = CompletedEliminatedHumanMatch(humanPortrait: 4);

        var notice = EndgameNoticePresentation.For(state);

        Assert.NotNull(notice);
        Assert.Equal(EndgameNoticeKind.Elimination, notice.Kind);
        Assert.Equal(4, notice.PortraitId);
        Assert.False(EndgameNoticePresentation.ContinuesToSummary(notice.Kind));
    }

    [Fact]
    public void HotSeatMatchGoesDirectlyToSharedAwardsScreen()
    {
        var state = CompletedMatch(ScenarioId.Greed, humanPortrait: 2, humanCount: 2);

        Assert.Null(EndgameNoticePresentation.For(state));
    }

    [Fact]
    public void EndgameRowsFollowAuthoritativeCompetitionStandings()
    {
        var state = CompletedMatch(ScenarioId.Greed, humanPortrait: 3);

        var rows = EndgamePresentation.Rows(state);

        Assert.Equal(state.Outcome!.Standings.Select(standing => standing.Player),
            rows.Select(row => row.Player));
        Assert.All(state.Outcome.Standings.Zip(rows), pair =>
            Assert.StartsWith($"{pair.First.Place}. ", pair.Second.Label));
    }

    [Fact]
    public void EndgameAtlasLayoutProvidesSixRowsFiveAwardsAndExactControls()
    {
        Assert.Equal(new Rectangle(0, 50, 428, 410), EndgameLayout.Panel);
        Assert.Equal(new Rectangle(320, 50, 50, 56), EndgameLayout.Awards);
        Assert.Equal(new Rectangle(372, 50, 52, 56), EndgameLayout.Stats);
        Assert.Equal(new Rectangle(320, 402, 104, 58), EndgameLayout.Done);
        Assert.Equal(new Rectangle(4, 382, 64, 64), EndgameLayout.Portrait(5));
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

    private static MatchState CompletedEliminatedHumanMatch(short humanPortrait)
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
                [new MatchGangState(new GangId(10), new PlayerId(0), 0, 0, 0)]),
            new(setups[1], 20,
                [new MatchGangState(new GangId(20), new PlayerId(1), 0, 1, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 1 ? new PlayerId(1) : null))
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
