using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// What the lobby says about the match everyone in it is about to play.
/// </summary>
/// <remarks>
/// A seated player could previously read who else was here and nothing at all about the game: the
/// scenario, its length, how the computer players think and whether turns are timed were all behind
/// a button only the host could press. They are settings, not secrets, and the people deciding
/// whether to stay are owed them. Kept apart from the drawing so the wording can be asserted without
/// a graphics device.
/// </remarks>
public static class OnlineLobbySummary
{
    /// <summary>One label and one value per line, in the order the lobby draws them.</summary>
    public static IReadOnlyList<(string Label, string Value)> Rows(
        ScenarioId scenario,
        GameDuration duration,
        AiDifficulty mentality,
        PlanningTimeLimit turnTimer) =>
    [
        ("SCENARIO", ScenarioCatalog.Get(scenario).Name),
        ("LENGTH", DurationSetupTooltip.Label(duration)),
        ("OPPONENTS", DifficultyPresentation.Label(mentality)),
        ("TURN TIMER", PlanningTimerPolicy.Label(turnTimer))
    ];
}
