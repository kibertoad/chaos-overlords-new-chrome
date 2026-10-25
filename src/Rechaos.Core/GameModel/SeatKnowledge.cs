namespace Rechaos.Core.GameModel;

/// <summary>
/// Whether a match state tells the player who plans on it a fact, for the rebuild's own
/// presentation that must not draw a neutral value a seat's view stands in for a hidden one as if
/// it were true (docs/MULTIPLAYER.md, "Planning on a view"). A whole match
/// (<see cref="MatchState.ViewedBy"/> null), as in single-player, hot-seat and lockstep play, holds
/// every fact, so every answer is true. On a view each answer is true for exactly the parts
/// <see cref="SeatView.Project"/> keeps as they are in the whole match.
/// </summary>
public static class SeatKnowledge
{
    /// <summary>
    /// The progress, completion and influence of <paramref name="sector"/>'s sites, and the parts
    /// they make of its Support, Cash and Tolerance, which leave its base Tolerance. A view keeps
    /// them only in the seat's own sectors (RULE-UI-011, SCR-UI-004, SCR-UI-007); elsewhere it
    /// holds no progress and a base equal to the Tolerance the console shows. In its own sectors
    /// the seat can work the base out as the Tolerance less the completed sites' part
    /// (RULE-SITE-001).
    /// </summary>
    public static bool KnowsSites(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        return state.ViewedBy is not { } seat || sector.Owner == seat;
    }

    /// <summary>
    /// <paramref name="player"/>'s cash, Support, Big Man points, statistics and exact scenario
    /// score. The console shows a seat only its own (SCR-UI-003); a view holds 0 for the other
    /// seats' totals and scores that only place their portraits on the rankings rail
    /// (SCR-OBJECTIVE-001).
    /// </summary>
    public static bool KnowsTotals(MatchState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.ViewedBy is not { } seat || player == seat;
    }
}
