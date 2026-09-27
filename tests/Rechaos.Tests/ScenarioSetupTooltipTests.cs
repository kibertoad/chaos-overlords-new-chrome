using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ScenarioSetupTooltipTests
{
    [Fact]
    public void EveryScenarioExplainsItsObjectiveAndDistinctiveRules()
    {
        foreach (var scenario in Enum.GetValues<ScenarioId>())
        {
            var lines = ScenarioSetupTooltip.Lines(scenario, GameDuration.SixMonths);

            Assert.Equal(ScenarioCatalog.Get(scenario).Name, lines[0]);
            Assert.True(lines.Count >= 4);
            Assert.All(lines, line => Assert.False(string.IsNullOrWhiteSpace(line)));
        }
    }

    // RULE-OBJECTIVE-004: the timed scenarios name the turn they end after, and every tooltip says
    // what else ends the match.
    [Fact]
    public void TooltipsStateTheEndConditionTheOriginalTests()
    {
        Assert.Contains("ENDS AFTER TURN 52: THE HIGHEST CASH WINS.",
            ScenarioSetupTooltip.Lines(ScenarioId.Greed, GameDuration.OneYear));
        Assert.Contains("IT HAS NO OTHER END TEST.",
            ScenarioSetupTooltip.Lines(ScenarioId.Eliminate, GameDuration.OneYear));
        Assert.Contains("IT ALSO ENDS WHEN ONE OVERLORD IS LEFT.",
            ScenarioSetupTooltip.Lines(ScenarioId.Big40, GameDuration.OneYear));
    }

    [Fact]
    public void DominanceTooltipUsesTheSelectedDurationWeights()
    {
        var lines = ScenarioSetupTooltip.Lines(ScenarioId.Dominance, GameDuration.FourYears);

        Assert.Contains(lines, line => line.Contains("CASH 1"));
        Assert.Contains(lines, line => line.Contains("SUPPORT 300"));
        Assert.Contains(lines, line => line.Contains("SECTOR 1000"));
    }

    [Fact]
    public void ArmageddonTooltipCallsOutItsDifferentStartingState()
    {
        var lines = ScenarioSetupTooltip.Lines(ScenarioId.Armageddon, GameDuration.SixMonths);

        Assert.Contains(lines, line => line.Contains("$500"));
        Assert.Contains(lines, line => line.Contains("ALL ITEMS RESEARCHED"));
    }

    [Fact]
    public void KillEmAllTooltipSpellsOutWhatDestroyingARivalTakes()
    {
        var lines = ScenarioSetupTooltip.Lines(ScenarioId.KillEmAll, GameDuration.SixMonths);

        Assert.Contains(lines, line => line.Contains("LAST GANG DIES"));
        Assert.Contains(lines, line => line.Contains("OWN NO SECTOR"));
    }

    [Theory]
    [InlineData(GameDuration.SixMonths, 26)]
    [InlineData(GameDuration.OneYear, 52)]
    [InlineData(GameDuration.TwoYears, 104)]
    [InlineData(GameDuration.FourYears, 208)]
    public void DurationTooltipExplainsExactTurnLimit(GameDuration duration, int turns)
    {
        var lines = DurationSetupTooltip.Lines(duration);

        Assert.Contains($"{turns} TURNS.", lines);
        Assert.Contains(lines, line => line.Contains("OBJECTIVE MODES"));
    }
}
