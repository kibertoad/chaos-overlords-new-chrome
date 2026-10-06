using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OnlineLobbySummaryTests
{
    [Fact]
    public void ItNamesTheScenarioItsLengthTheOpponentsTheClockAndWhoMayWatch()
    {
        var rows = OnlineLobbySummary.Rows(
            ScenarioId.KillEmAll, GameDuration.TwoYears,
            AiDifficulty.CrimeLord, PlanningTimeLimit.TwoMinutes, spectatorDelayTurns: 3);

        Assert.Equal(
            ["SCENARIO", "LENGTH", "OPPONENTS", "TURN TIMER", "SPECTATORS"],
            rows.Select(row => row.Label));
        Assert.Equal(
            [
                ScenarioCatalog.Get(ScenarioId.KillEmAll).Name, "2 YEARS", "CRIME LORD", "2 MINUTES",
                "3 TURNS BEHIND",
            ],
            rows.Select(row => row.Value));
        Assert.Equal(OnlineLobbySummary.RowCount, rows.Count);
        Assert.Equal("SPECTATORS", rows[OnlineLobbySummary.SpectatorRow].Label);
    }

    [Fact]
    public void AMatchNobodyMayWatchSaysSo()
    {
        var rows = OnlineLobbySummary.Rows(
            ScenarioId.KillEmAll, GameDuration.TwoYears,
            AiDifficulty.CrimeLord, PlanningTimeLimit.None, spectatorDelayTurns: null);

        Assert.Equal("NOT ALLOWED", rows[OnlineLobbySummary.SpectatorRow].Value);
    }

    /// <summary>
    /// The host's arrows hold every value between them, clear of the label, and the summary's
    /// fifth row still ends above the button under it.
    /// </summary>
    [Fact]
    public void TheHostsSpectatorControlFitsItsRow()
    {
        var row = OnlineLobbyLayout.SummaryRow(OnlineLobbySummary.SpectatorRow);
        var earlier = OnlineLobbyLayout.SpectatorDelayEarlier;
        var later = OnlineLobbyLayout.SpectatorDelayLater;
        Assert.True(row.X + "SPECTATORS".Length * OriginalFontLayout.CellWidth < earlier.X);
        Assert.Equal(row.Right, later.Right);
        var choices = Enumerable.Range(
                SpectatorDelayChoice.Minimum,
                SpectatorDelayChoice.Maximum - SpectatorDelayChoice.Minimum + 1)
            .Select(turns => (int?)turns)
            .Prepend(null);
        foreach (var delay in choices)
        {
            var width = SpectatorDelayChoice.Label(delay).Length * OriginalFontLayout.CellWidth;
            Assert.True(OnlineLobbyLayout.SpectatorValueRight - width >= earlier.Right,
                $"{SpectatorDelayChoice.Label(delay)} runs into the left arrow");
        }
        Assert.True(later.Bottom <= OnlineLobbyLayout.Setup.Y);
        Assert.True(
            OnlineLobbyLayout.SummaryRow(OnlineLobbySummary.RowCount - 1).Bottom
                <= OnlineLobbyLayout.Setup.Y);
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
        foreach (var (label, value) in OnlineLobbySummary.Rows(
                     scenario, duration, mentality, timer, SpectatorDelayChoice.Maximum))
        {
            var used = (label.Length + value.Length + 2) * OriginalFontLayout.CellWidth;
            Assert.True(used <= row.Width, $"{label} {value} needs {used} of {row.Width}");
        }
    }

    /// <summary>
    /// The stand-in for a settings blob this build cannot read fits where the rows would be.
    /// </summary>
    /// <remarks>
    /// It is drawn from the left of a summary row, one line per row, so a line too wide for the
    /// column would run out through the panel's border. There are five rows to spend.
    /// </remarks>
    [Fact]
    public void TheUnreadableNoticeFitsTheRowsItStandsIn()
    {
        var row = OnlineLobbySummary.Rows(
            ScenarioId.KillEmAll, GameDuration.TwoYears,
            AiDifficulty.CrimeLord, PlanningTimeLimit.None, spectatorDelayTurns: null).Count;
        Assert.InRange(OnlineLobbySummary.Unreadable.Count, 1, row);
        foreach (var line in OnlineLobbySummary.Unreadable)
        {
            var used = line.Length * OriginalFontLayout.CellWidth;
            var width = OnlineLobbyLayout.SummaryRow(0).Width;
            Assert.True(used <= width, $"{line} needs {used} of {width}");
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
