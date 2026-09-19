using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OnlineLobbySummaryTests
{
    [Fact]
    public void ItNamesTheScenarioItsLengthTheOpponentsAndTheClock()
    {
        var rows = OnlineLobbySummary.Rows(
            ScenarioId.KillEmAll, GameDuration.TwoYears,
            AiDifficulty.CrimeLord, PlanningTimeLimit.TwoMinutes);

        Assert.Equal(
            ["SCENARIO", "LENGTH", "OPPONENTS", "TURN TIMER"],
            rows.Select(row => row.Label));
        Assert.Equal(
            [ScenarioCatalog.Get(ScenarioId.KillEmAll).Name, "2 YEARS", "CRIME LORD", "2 MINUTES"],
            rows.Select(row => row.Value));
    }

    /// <summary>
    /// Every value fits the half of the row it is drawn in.
    /// </summary>
    /// <remarks>
    /// The label is drawn from the left and the value from the right of the same line, so a value
    /// that outgrew its half would be drawn through the label rather than clipped.
    /// </remarks>
    [Theory]
    [MemberData(nameof(EverySetting))]
    public void EveryValueFitsBesideItsLabel(
        ScenarioId scenario, GameDuration duration, AiDifficulty mentality, PlanningTimeLimit timer)
    {
        var row = OnlineLobbyLayout.SummaryRow(0);
        foreach (var (label, value) in OnlineLobbySummary.Rows(scenario, duration, mentality, timer))
        {
            var used = (label.Length + value.Length + 2) * OriginalFontLayout.CellWidth;
            Assert.True(used <= row.Width, $"{label} {value} needs {used} of {row.Width}");
        }
    }

    public static TheoryData<ScenarioId, GameDuration, AiDifficulty, PlanningTimeLimit>
        EverySetting()
    {
        var data = new TheoryData<ScenarioId, GameDuration, AiDifficulty, PlanningTimeLimit>();
        foreach (var scenario in Enum.GetValues<ScenarioId>())
            foreach (var duration in Enum.GetValues<GameDuration>())
                foreach (var mentality in Enum.GetValues<AiDifficulty>())
                    foreach (var timer in Enum.GetValues<PlanningTimeLimit>())
                        data.Add(scenario, duration, mentality, timer);
        return data;
    }
}
