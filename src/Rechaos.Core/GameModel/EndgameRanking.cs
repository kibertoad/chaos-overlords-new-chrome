namespace Rechaos.Core.GameModel;

public sealed record MatchStanding(PlayerId Player, int Place, long Score);

/// <summary>
/// Produces timed-scenario standings from the manual-defined score. Ordering
/// within a tied place is stable by player ID; original presentation is unverified.
/// </summary>
public static class EndgameRankingEvaluator
{
    public static IReadOnlyList<MatchStanding> EvaluateTimed(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ScenarioCatalog.Get(state.Setup.Scenario).IsTimed)
            throw new ArgumentException("Only timed scenarios have a verified ranking score.", nameof(state));

        var ordered = state.Players
            .Select(player => new MatchStanding(
                player.Id,
                Place: 0,
                ScenarioCatalog.TimedScore(
                    state.Setup.Scenario,
                    state.Setup.Duration,
                    MatchOutcomeEvaluator.Project(state, player))))
            .OrderByDescending(standing => standing.Score)
            .ThenBy(standing => standing.Player.Value)
            .ToArray();
        var standings = new MatchStanding[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            var place = index == 0 || ordered[index].Score != ordered[index - 1].Score
                ? index + 1
                : standings[index - 1].Place;
            standings[index] = ordered[index] with { Place = place };
        }
        return standings;
    }
}
