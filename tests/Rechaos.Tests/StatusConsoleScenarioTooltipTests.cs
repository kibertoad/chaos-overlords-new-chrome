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
}
