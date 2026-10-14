using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class BoardViewerTests
{
    [Fact]
    public void ComputerPlanningKeepsTheLastLocalHumansBoard()
    {
        var (match, recorder) = CreateMatch(
            PlayerController.Human, PlayerController.Computer,
            PlayerController.Human, PlayerController.Computer);

        recorder.FinishUpkeep();
        Assert.Equal(new PlayerId(0), BoardViewer.For(match, match.Coordinator.ActivePlayer, online: false));

        // The computer in slot 1 holds the turn: the board stays with the human in slot 0.
        recorder.FinishCommand(new PlayerId(0));
        Assert.Equal(new PlayerId(0), BoardViewer.For(match, match.Coordinator.ActivePlayer, online: false));

        recorder.FinishCommand(new PlayerId(1));
        Assert.Equal(new PlayerId(2), BoardViewer.For(match, match.Coordinator.ActivePlayer, online: false));

        // Slot 3 looks back past the computer to the human in slot 2.
        recorder.FinishCommand(new PlayerId(2));
        Assert.Equal(new PlayerId(3), match.Coordinator.ActivePlayer);
        Assert.Equal(new PlayerId(2), BoardViewer.For(match, match.Coordinator.ActivePlayer, online: false));

        // Online the client draws what it was given, and a final view or no active player is kept.
        Assert.Equal(new PlayerId(3), BoardViewer.For(match, new PlayerId(3), online: true));
        Assert.Null(BoardViewer.For(match, null, online: false));
    }

    [Fact]
    public void AComputerInTheFirstSlotLooksBackToTheLastHumanSeat()
    {
        var (match, recorder) = CreateMatch(PlayerController.Computer, PlayerController.Human);

        recorder.FinishUpkeep();
        Assert.Equal(new PlayerId(0), match.Coordinator.ActivePlayer);
        Assert.Equal(new PlayerId(1), BoardViewer.For(match, match.Coordinator.ActivePlayer, online: false));
    }

    [Fact]
    public void AMatchWithoutHumansDrawsTheActiveComputer()
    {
        var (match, recorder) = CreateMatch(PlayerController.Computer, PlayerController.Computer);

        recorder.FinishUpkeep();
        Assert.Equal(new PlayerId(0), BoardViewer.For(match, match.Coordinator.ActivePlayer, online: false));
    }

    private static (MatchState Match, MatchReplayRecorder Recorder) CreateMatch(params PlayerController[] controllers)
    {
        var data = BundledOriginalData.Load();
        var setups = controllers
            .Select((controller, index) => new MatchPlayerSetup(new PlayerId(index), $"P{index}", controller))
            .ToArray();
        var players = setups
            .Select((setup, index) => new MatchPlayerState(setup, 20,
                [new MatchGangState(new GangId(10 + index), setup.Id, (short)(1 + index), index, 5)]))
            .ToArray();
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: null, income: 3))
            .ToArray();
        var match = new MatchState(data, new MatchSetup(
            ScenarioId.Eliminate, GameDuration.SixMonths, 37, setups, MatchDeviations.Original), players, sectors);
        return (match, new MatchReplayRecorder(match));
    }
}
