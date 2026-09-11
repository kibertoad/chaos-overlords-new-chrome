namespace Rechaos.Core.GameModel;

public sealed record MatchStanding(PlayerId Player, int Place, long Score);

/// <summary>
/// Produces the original scenario standings. Active players use competition
/// places and stable player-slot tie order; eliminated players follow unranked.
/// </summary>
public static class EndgameRankingEvaluator
{
    public static long Score(MatchState state, MatchPlayerState player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        if (state.FindPlayer(player.Id) != player)
            throw new ArgumentException("Player does not belong to the match.", nameof(player));
        return OriginalAiScenarioStandingRules.Score(state, player);
    }

    public static IReadOnlyList<MatchStanding> Evaluate(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return OriginalAiScenarioStandingRules.Rank(state);
    }

    public static IReadOnlyList<MatchStanding> EvaluateTimed(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!ScenarioCatalog.Get(state.Setup.Scenario).IsTimed)
            throw new ArgumentException("Scenario is not timed.", nameof(state));
        return Evaluate(state);
    }
}
