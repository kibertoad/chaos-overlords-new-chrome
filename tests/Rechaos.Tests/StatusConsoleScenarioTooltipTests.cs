using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class StatusConsoleScenarioTooltipTests
{
    [Fact]
    public void PlayingModeTooltipMatchesNewGameDescription()
    {
        foreach (var scenario in Enum.GetValues<ScenarioId>())
        {
            var expected = ScenarioSetupTooltip.Lines(scenario, GameDuration.TwoYears);
            var actual = StatusConsoleTooltip.At(
                StatusConsoleLayout.Scenario.Center, scenario, GameDuration.TwoYears);

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void ScoreTooltipStatesWhatTheScenarioRates()
    {
        foreach (var scenario in Enum.GetValues<ScenarioId>())
        {
            var point = StatusConsoleLayout.Score.Center;
            var lines = StatusConsoleTooltip.At(point, scenario, GameDuration.TwoYears);

            Assert.Equal("SCORE", lines[0]);
            Assert.Contains(PlayerRankingTooltip.Basis(scenario), lines[1]);
            Assert.DoesNotContain(lines, line => line.Contains("VICTORY"));
            Assert.Equal(scenario is ScenarioId.KillEmAll or ScenarioId.Siege,
                lines.Contains("SHARED BY EVERY SURVIVING OVERLORD."));
            Assert.True(lines.Max(line => line.Length) * OriginalFontLayout.CellWidth + 16
                <= VirtualInput.Width - 16);
        }
    }

    [Fact]
    public void TolerancePresentationRelatesQueuedChaosRangeToThreshold()
    {
        var range = new ChaosRange(0, 9);
        var estimate = new ChaosRangeEstimate(range, []);
        string[] breakdown =
        [
            "TEST GANG: 9 D6 ROLLS",
            "  2 INCOME + 5 FORCE + 2 CHAOS"
        ];

        Assert.Equal(
            [
                "TOLERANCE",
                "CHAOS ABOVE THIS VALUE TRIGGERS",
                "A POLICE CRACKDOWN.",
                "",
                "YOUR QUEUED CHAOS: 0-9",
                "",
                "SUCCESS RANGE FROM YOUR QUEUED ORDERS.",
                "EACH POINT ROLLS ONE STANDARD SIX-SIDED DIE.",
                "ONLY ROLLS OF 5+ ADD CHAOS AND CASH.",
                "ITEMS AND LOCAL SITES MODIFY THE ROLL POOL.",
                "",
                "CONTROLLED: EACH SUCCESS PAYS $1.",
                "UNCONTROLLED: HALF THE COMBINED",
                "SUCCESSES, ROUNDED DOWN.",
                "CRACKDOWN THIS TURN: NO CHAOS CASH.",
                "",
                "YOUR RANGE CANNOT TRIGGER A CRACKDOWN.",
                "THE THIRD CRACKDOWN IN 5 TURNS MAKES",
                "THE SECTOR NEUTRAL AND BRINGS POLICE",
                "FOR 3-5 TURNS. POLICE DO NOT STOP PAY.",
                "",
                "CHAOS RANGE BREAKDOWN:",
                ..breakdown
            ],
            StatusConsoleTooltip.Tolerance(12, estimate, breakdown));
        Assert.Equal(Color.Lime, StatusConsolePresentation.QueuedChaosRangeColor(range, 12));
        Assert.Equal(Color.Red, StatusConsolePresentation.QueuedChaosRangeColor(range, 8));
        Assert.Contains(StatusConsoleTooltip.EnemyChaosWarning,
            StatusConsoleTooltip.Tolerance(12, estimate, breakdown, enemyGangsPresent: true));
    }
}
