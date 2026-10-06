using Rechaos.Multiplayer.Generated;

namespace Rechaos.Game;

/// <summary>
/// What the spectator view says about where the watched match stands.
/// </summary>
/// <remarks>
/// <para>
/// The server releases a turn to spectators only once the players are <c>delay</c> turns past it,
/// so a spectator always reads two numbers: the turn the shown city is being planned for, and the
/// turn the players are on. Both are drawn, with the delay beside them, so nobody mistakes the
/// shown city for the live one.
/// </para>
/// <para>
/// <c>ShownTurn</c> is the last turn the shown state has resolved, so the city is the one players
/// planned turn <c>ShownTurn + 1</c> from. The live turn is the match's open turn.
/// </para>
/// </remarks>
public static class SpectatorViewPresentation
{
    /// <summary>The three lines of the panel beside the map.</summary>
    /// <param name="Progress">Which turn is shown, and which the players are on.</param>
    /// <param name="Delay">How far behind the players the view is held.</param>
    /// <param name="Standing">What the view is waiting for, or that it is following.</param>
    public sealed record Lines(string Progress, string Delay, string Standing);

    public const string Joining = "ASKING TO WATCH";

    /// <summary>The lines for a view that has heard from the server at least once.</summary>
    /// <param name="view">The match as the server last described it.</param>
    /// <param name="hasState">Whether a city can be drawn.</param>
    /// <param name="shownTurn">The last turn the shown city has resolved.</param>
    /// <param name="isComplete">Whether nothing more will be released.</param>
    /// <param name="connected">Whether the last poll was answered.</param>
    public static Lines Describe(
        SpectatorMatchView view, bool hasState, int? shownTurn, bool isComplete, bool connected)
    {
        ArgumentNullException.ThrowIfNull(view);
        var delay = $"{view.DelayTurns} TURNS BEHIND";
        var progress = hasState && shownTurn is { } shown
            ? $"TURN {shown + 1} OF {Math.Max(view.CurrentTurn, shown + 1)}"
            : view.Status == MatchStatus.Lobby
                ? "NOT STARTED"
                : $"PLAYERS ON TURN {view.CurrentTurn}";
        return new Lines(progress, delay, Standing(view, hasState, isComplete, connected));
    }

    private static string Standing(
        SpectatorMatchView view, bool hasState, bool isComplete, bool connected)
    {
        if (isComplete)
            return view.Status == MatchStatus.Abandoned ? "THE MATCH WAS ABANDONED" : "THE MATCH IS OVER";
        if (!connected) return "CONNECTION LOST  RETRYING";
        if (view.Status == MatchStatus.Lobby) return "WAITING FOR THE HOST TO START";
        // A running match shows nothing until the host has stored the city it began from.
        if (!hasState) return "WAITING FOR THE STARTING CITY";
        if (view.Status is MatchStatus.Finished or MatchStatus.Abandoned) return "SHOWING THE LAST TURNS";
        return "FOLLOWING THE MATCH";
    }
}
