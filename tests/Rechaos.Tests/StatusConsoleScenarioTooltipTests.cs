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
    public void TolerancePresentationRelatesQueuedChaosRangeToThreshold()
    {
        var range = new ChaosRange(0, 9);
        var estimate = new ChaosRangeEstimate(range, []);
        string[] breakdown =
        [
            "TEST GANG: 9 CHAOS DICE",
            "  2 INCOME + 5 FORCE + 2 CHAOS"
        ];

        Assert.Equal(
            [
                "TOLERANCE",
                "CHAOS ABOVE THIS VALUE TRIGGERS",
                "A POLICE CRACKDOWN.",
                "",
                "YOUR QUEUED CHAOS: 0-9",
                "SUCCESS RANGE FROM YOUR QUEUED ORDERS.",
                "ITEMS AND LOCAL SITES MODIFY CHAOS.",
                "CONTROLLED: EACH SUCCESS PAYS $1.",
                "UNCONTROLLED: HALF THE COMBINED",
                "SUCCESSES, ROUNDED DOWN.",
                "CRACKDOWN: NO CHAOS CASH PAID.",
                "",
                "YOUR RANGE CANNOT TRIGGER A CRACKDOWN.",
                "",
                "CHAOS RANGE BREAKDOWN:",
                ..breakdown
            ],
            StatusConsoleTooltip.Tolerance(12, estimate, breakdown));
        Assert.Contains("KNOWN ENEMY GANGS MAY ADD MORE CHAOS.",
            StatusConsoleTooltip.Tolerance(12, estimate, breakdown, knownEnemyGangs: true));
    }
}
