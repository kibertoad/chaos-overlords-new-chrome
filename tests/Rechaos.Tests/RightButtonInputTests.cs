using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// FND-UI-063: the city takes the right button only on its console tiles, which it holds until the
/// right button comes up, and the sector view's back control acts at a right press because its
/// helper waits only on the left button. The rebuild's other right-button cancels are DEV-UI-010.
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
