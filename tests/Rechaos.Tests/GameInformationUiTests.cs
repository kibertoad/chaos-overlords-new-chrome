using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GameInformationUiTests
{
    [Fact]
    public void LayoutMatchesScenarioInformationTemplate()
    {
        Assert.Equal(new Rectangle(104, 125, 344, 209), GameInformationLayout.Panel);
        Assert.Equal(new Rectangle(136, 294, 49, 24), GameInformationLayout.Ok);
        Assert.Equal(204, GameInformationLayout.ValueLeft);
        Assert.Equal(216, GameInformationLayout.PlayerNameLeft);
        Assert.Equal([152, 170, 188],
            [GameInformationLayout.ObjectiveY, GameInformationLayout.AiMentalityY,
                GameInformationLayout.TurnTimeLimitY]);
        Assert.Equal(new Rectangle(204, 215, 5, 7), GameInformationLayout.PlayerColor(0));
        Assert.Equal(new Rectangle(204, 260, 5, 7), GameInformationLayout.PlayerColor(5));
        Assert.Equal([215, 224, 233, 242, 251, 260],
            Enumerable.Range(0, MatchLimits.PlayerCount).Select(GameInformationLayout.PlayerY));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameInformationLayout.PlayerY(6));
    }

    [Theory]
    [InlineData(PlayerController.Human, "HUMAN")]
    [InlineData(PlayerController.Computer, "AI")]
    public void IntelligenceUsesManualDefinedLabels(PlayerController controller, string label)
    {
        Assert.Equal(label, GameInformationPresentation.Intelligence(controller));
    }

    [Fact]
    public void OnlyLocalMultiplayerAutomaticallyOpensAtNewGame()
    {
        Assert.False(GameInformationPresentation.OpensAtNewGame(Setup(PlayerController.Human)));
        Assert.True(GameInformationPresentation.OpensAtNewGame(
            Setup(PlayerController.Human, PlayerController.Human)));
        Assert.False(GameInformationPresentation.OpensAtNewGame(
            Setup(PlayerController.Human, PlayerController.Computer)));
    }

    [Fact]
    public void GameInformationIsAStandardSlidingPanel()
    {
        Assert.True(PanelSlideTransition.IsPanel(ClientScreen.GameInfo));
        var router = new ScreenRouter();
        router.Show(ClientScreen.GameInfo);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
    }

    private static MatchSetup Setup(params PlayerController[] controllers) => new(
        ScenarioId.Greed,
        GameDuration.SixMonths,
        1,
        controllers.Select((controller, index) => new MatchPlayerSetup(
            new PlayerId(index), $"PLAYER#{index + 1}", controller, (short)index)).ToArray());
}
