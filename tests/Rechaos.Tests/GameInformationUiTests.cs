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
        Assert.Equal(new Rectangle(104, 124, 344, 209), GameInformationLayout.InputBounds);
        Assert.Equal(228, GameInformationLayout.ValueLeft);
        Assert.Equal(240, GameInformationLayout.PlayerNameLeft);
        Assert.Equal([151, 169, 187],
            [GameInformationLayout.ObjectiveY, GameInformationLayout.AiMentalityY,
                GameInformationLayout.TurnTimeLimitY]);
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
    [InlineData(ScenarioId.Big40, GameDuration.OneYear, "THE BIG 40")]
    [InlineData(ScenarioId.Eliminate, GameDuration.SixMonths, "ELIMINATE")]
    public void ScenarioLabelMatchesNativeTimedObjectivePresentation(
        ScenarioId scenario, GameDuration duration, string expected)
    {
        Assert.Equal(expected, GameInformationPresentation.ScenarioLabel(scenario, duration));
    }

    // RULE-UI-009: the Mentality is the original's whole name, and DEV-AI-003 adds the policy only
    // with Advanced AI on.
    [Theory]
    [InlineData(AiDifficulty.Goon, AiPolicyMode.Original, "GOON")]
    [InlineData(AiDifficulty.HomicidalManiac, AiPolicyMode.Original, "HOMICIDAL MANIAC")]
    [InlineData(AiDifficulty.CrimeLord, AiPolicyMode.Advanced, "CRIME LORD ADVANCED")]
    public void MentalityFieldIsTheOriginalsUnlessAdvancedAiIsOn(
        AiDifficulty mentality, AiPolicyMode policy, string expected)
    {
        Assert.Equal(expected, GameInformationPresentation.MentalityField(mentality, policy));
    }

    [Theory]
    [InlineData(PlanningTimeLimit.None, "NONE")]
    [InlineData(PlanningTimeLimit.ThirtySeconds, "30 SECONDS")]
    [InlineData(PlanningTimeLimit.TwoMinutes, "2 MINUTES")]
    [InlineData(PlanningTimeLimit.FiveMinutes, "5 MINUTES")]
    public void PlanningLimitFollowsRuleUi009(PlanningTimeLimit limit, string expected)
    {
        Assert.Equal(expected, GameInformationPresentation.PlanningLimit(limit));
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
