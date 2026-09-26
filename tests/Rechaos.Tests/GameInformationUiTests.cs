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
        Assert.Equal(new Rectangle(128, 124, 320, 209), GameInformationLayout.Panel);
        Assert.Equal(new Rectangle(0, 0, 320, 209), GameInformationLayout.BackgroundSource);
        Assert.Equal(new Rectangle(161, 293, 49, 22), GameInformationLayout.Ok);
        Assert.Equal(228, GameInformationLayout.ValueLeft);
        Assert.Equal(240, GameInformationLayout.PlayerNameLeft);
        Assert.Equal([151, 169, 187],
            [GameInformationLayout.ObjectiveY, GameInformationLayout.AiMentalityY,
                GameInformationLayout.TurnTimeLimitY]);
        Assert.Equal(new Rectangle(228, 214, 5, 7), GameInformationLayout.PlayerColor(0));
        Assert.Equal(new Rectangle(228, 259, 5, 7), GameInformationLayout.PlayerColor(5));
        Assert.Equal([214, 223, 232, 241, 250, 259],
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

    [Theory]
    [InlineData(PlayerController.Human, PlayerStatus.Active, "HUMAN")]
    [InlineData(PlayerController.Computer, PlayerStatus.Active, "AI")]
    [InlineData(PlayerController.Human, PlayerStatus.Eliminated, "ELIMINATED")]
    [InlineData(PlayerController.Computer, PlayerStatus.Eliminated, "ELIMINATED")]
    public void PlayerLabelGivesEliminationPrecedenceOverController(
        PlayerController controller, PlayerStatus status, string label)
    {
        Assert.Equal(label, GameInformationPresentation.PlayerLabel(controller, status));
    }

    [Theory]
    [InlineData(ScenarioId.Greed, GameDuration.SixMonths, "GREED (6 MONTHS)")]
    [InlineData(ScenarioId.Power, GameDuration.OneYear, "POWER (1 YEAR)")]
    [InlineData(ScenarioId.Acceptance, GameDuration.TwoYears, "ACCEPTANCE (2 YEARS)")]
    [InlineData(ScenarioId.Dominance, GameDuration.FourYears, "DOMINANCE (4 YEARS)")]
    [InlineData(ScenarioId.Siege, GameDuration.FourYears, "SIEGE")]
    public void ScenarioLabelMatchesNativeTimedObjectivePresentation(
        ScenarioId scenario, GameDuration duration, string expected)
    {
        Assert.Equal(expected, GameInformationPresentation.ScenarioLabel(scenario, duration));
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
}
