using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// FND-UI-063: the city takes the right button only on its console tiles, which it holds until the
/// right button comes up, and the sector view's back control acts at a right press because its
/// helper waits only on the left button. On the sector view (SCR-UI-004) the second press of a
/// right double-click reaches only the console tiles. The rebuild's other right-button cancels are DEV-UI-010.
/// </summary>
public sealed class RightButtonInputTests
{
    [Fact]
    public void ARightPressHoldsACityConsoleTileUntilTheRightButtonComesUp()
    {
        var game = CityGame();
        var ranking = CityConsoleLayout.Ranking.Center;

        RightPress(game, ranking);
        Assert.Equal(CityConsoleControl.RankingSearch, Field<CityConsoleControl?>(game, "_pressedCityConsoleControl"));

        // A left release does not let go of a tile the right button holds.
        Release(game, ranking, rightButton: false);
        Assert.Equal(ClientScreen.City, Router(game).Current);
        Assert.NotNull(Field<CityConsoleControl?>(game, "_pressedCityConsoleControl"));

        Release(game, ranking, rightButton: true);
        Assert.Null(Field<CityConsoleControl?>(game, "_pressedCityConsoleControl"));
        Assert.Equal(ClientScreen.Ranking, Router(game).Current);
    }

    [Fact]
    public void ATileHoldOfTheRightButtonThatEscapeLetsGoOfLastsUntilTheRightButtonComesUp()
    {
        var game = CityGame();
        var ranking = CityConsoleLayout.Ranking.Center;
        RightPress(game, ranking);
        DeviationBehaviourTests.Field("_previousMouse").SetValue(game, new MouseState(
            ranking.X, ranking.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed,
            ButtonState.Released, ButtonState.Released));

        DeviationBehaviourTests.Call(game, "CancelCurrentInteraction", (Point?)null);

        Assert.Null(Field<CityConsoleControl?>(game, "_pressedCityConsoleControl"));
        Assert.True(Field<bool>(game, "_rightHoldOutlivesCancel"));
        Assert.True((bool)DeviationBehaviourTests.Call(game, "HoldsCityPointer")!);
    }

    [Fact]
    public void ARightReleaseOutsideTheTileActsOnNothing()
    {
        var game = CityGame();

        RightPress(game, CityConsoleLayout.Ranking.Center);
        Release(game, new Point(10, 450), rightButton: true);

        Assert.Null(Field<CityConsoleControl?>(game, "_pressedCityConsoleControl"));
        Assert.Equal(ClientScreen.City, Router(game).Current);
    }

    [Fact]
    public void TheCityMapIgnoresTheRightButton()
    {
        var game = CityGame();
        var cursor = Field<int>(game, "_cursor");

        RightPress(game, new Point(300, 300));

        Assert.Null(Field<CityConsoleControl?>(game, "_pressedCityConsoleControl"));
        Assert.Equal(cursor, Field<int>(game, "_cursor"));
        Assert.Equal(ClientScreen.City, Router(game).Current);
    }

    [Fact]
    public void ARightPressOnTheSectorBackControlReturnsAtOnce()
    {
        var game = CityGame();
        Router(game).Show(ClientScreen.Sector);

        RightPress(game, SectorDetailLayout.Back.Center);

        Assert.Equal(ClientScreen.City, Router(game).Current);
        Assert.Null(DeviationBehaviourTests.Field("_pressedPanelFace").GetValue(game));
    }

    [Fact]
    public void TheSecondPressOfASectorRightDoubleClickReachesOnlyTheConsole()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        var game = LeavePromptTests.GameFor(match, new MatchActions(new MatchReplayRecorder(match)), null);
        Router(game).Show(ClientScreen.Sector);
        var ranking = CityConsoleLayout.Ranking.Center;

        RightPress(game, ranking);
        Assert.Equal(CityConsoleControl.RankingSearch, Field<CityConsoleControl?>(game, "_pressedCityConsoleControl"));
        Release(game, new Point(10, 450), rightButton: true);
        Assert.Null(Field<CityConsoleControl?>(game, "_pressedCityConsoleControl"));

        // The second press arrives in the original as a right double-click, which the console
        // dispatcher takes as it takes a press.
        RightPress(game, ranking);
        Assert.Equal(CityConsoleControl.RankingSearch, Field<CityConsoleControl?>(game, "_pressedCityConsoleControl"));
        Assert.Equal(ClientScreen.Sector, Router(game).Current);
    }

    [Fact]
    public void ADoubleClickNeedsTheSecondPressCloseToTheFirstAndWithinTheWindow()
    {
        var clicks = new PointDoubleClickTracker();
        Assert.False(clicks.Register(new Point(100, 100), TimeSpan.FromSeconds(1)));
        Assert.True(clicks.Register(new Point(103, 97), TimeSpan.FromSeconds(1.4)));
        // A double-click ends the pair.
        Assert.False(clicks.Register(new Point(103, 97), TimeSpan.FromSeconds(1.5)));
        Assert.False(clicks.Register(new Point(107, 97), TimeSpan.FromSeconds(1.6)));
        Assert.False(clicks.Register(new Point(107, 97), TimeSpan.FromSeconds(2.2)));
        clicks.Cancel();
        Assert.False(clicks.Register(new Point(107, 97), TimeSpan.FromSeconds(2.3)));
    }

    private static ChaosGame CityGame()
    {
        var game = DeviationBehaviourTests.HeadlessGame();
        Router(game).Show(ClientScreen.City);
        return game;
    }

    private static void RightPress(ChaosGame game, Point point) =>
        DeviationBehaviourTests.Call(game, "CancelCurrentInteraction", (Point?)point);

    private static void Release(ChaosGame game, Point point, bool rightButton) =>
        DeviationBehaviourTests.Call(game, "CompletePointerRelease", true, point, rightButton);

    private static ScreenRouter Router(ChaosGame game) =>
        (ScreenRouter)DeviationBehaviourTests.Field("_screens").GetValue(game)!;

    private static T Field<T>(ChaosGame game, string name) =>
        (T)DeviationBehaviourTests.Field(name).GetValue(game)!;
}
