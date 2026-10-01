namespace Rechaos.Core.GameModel;

/// <summary>
/// Scenario scores and zero-based competition standings of the original routine at 0x0047712A,
/// which runs only when a match starts and at the end of each turn (FND-AI-005, FND-SETUP-015).
/// </summary>
internal static class OriginalAiScenarioStandingRules
{
    public const int InactiveStanding = byte.MaxValue;
    // FND-AI-005, BUG-OBJECTIVE-001: inactive slots still contribute this score when the original routine counts
    // strictly better scores. In Greed, active cash below -32,000 can therefore yield place 6.
    internal const int InactiveScore = -32_000;

    /// <summary>
    /// RULE-OBJECTIVE-002: stores every player's score in <see cref="MatchPlayerState.ScenarioScore"/>,
    /// and -32000 for a player no longer active. A new match passes <paramref name="cashBeforeModifier"/>:
    /// the original scores it before SMGFUNDAGE raises a player's cash to 1,500 (FND-SETUP-015).
    /// </summary>
    internal static void Record(MatchState state, Func<MatchPlayerState, int>? cashBeforeModifier = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var player in state.Players)
            player.ScenarioScore = player.Status == PlayerStatus.Active
                ? Score(state, player, cashBeforeModifier?.Invoke(player) ?? player.Cash)
                : InactiveScore;
    }

    /// <summary>
    /// RULE-OBJECTIVE-002: the standings of the scores the last evaluation stored, each the number of
    /// slots with a strictly greater score, and 0xFF for an inactive or empty slot.
    /// </summary>
    public static IReadOnlyList<int> Stored(MatchState state) =>
        Standings(state, player => player.ScenarioScore);

    public static IReadOnlyList<int> Build(MatchState state) =>
        Standings(state, player => Score(state, player));

    private static int[] Standings(MatchState state, Func<MatchPlayerState, int> score)
    {
        ArgumentNullException.ThrowIfNull(state);
        var scores = Enumerable.Repeat(InactiveScore, MatchLimits.PlayerCount).ToArray();
        foreach (var player in state.Players.Where(player => player.Status == PlayerStatus.Active))
            scores[player.Id.Value] = score(player);

        var standings = new int[MatchLimits.PlayerCount];
        for (var player = 0; player < MatchLimits.PlayerCount; player++)
        {
            if (state.FindPlayer(new PlayerId(player)) is not { Status: PlayerStatus.Active })
            {
                standings[player] = InactiveStanding;
                continue;
            }
            standings[player] = scores.Count(candidate => candidate > scores[player]);
        }
        return standings;
    }

    internal static IReadOnlyList<MatchStanding> Rank(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var originalStandings = Build(state);
        var ranked = state.Players
            .Where(player => player.Status == PlayerStatus.Active)
            .Select(player => new MatchStanding(
                player.Id,
                checked(originalStandings[player.Id.Value] + 1),
                Score(state, player)))
            .OrderBy(standing => standing.Place)
            .ThenBy(standing => standing.Player.Value)
            .ToArray();
        var standings = new List<MatchStanding>(ranked);
        standings.AddRange(state.Players
            .Where(player => player.Status != PlayerStatus.Active)
            .OrderBy(player => player.Id.Value)
            .Select(player => new MatchStanding(player.Id, 0, InactiveScore)));
        return standings;
    }

    internal static int Score(MatchState state, MatchPlayerState player) =>
        Score(state, player, player.Cash);

    private static int Score(MatchState state, MatchPlayerState player, int cash) =>
        state.Setup.Scenario switch
        {
            ScenarioId.Greed => cash,
            ScenarioId.Power or ScenarioId.Big40 or ScenarioId.Armageddon =>
                ControlledSectorCount(state, player.Id),
            ScenarioId.Acceptance => player.Support,
            ScenarioId.Dominance => DominanceScore(state, player, cash),
            ScenarioId.KillEmAll or ScenarioId.Eliminate =>
                MatchLimits.PlayerCount - state.Players.Count(candidate =>
                    candidate.Status == PlayerStatus.Active),
            ScenarioId.Siege => OriginalCityGenerator.HeadquartersCandidates.Count(
                sectorId => state.Sectors[sectorId].Owner == player.Id),
            ScenarioId.BigMan => player.BigManPoints,
            _ => throw new ArgumentOutOfRangeException()
        };

    private static int DominanceScore(MatchState state, MatchPlayerState player, int cash)
    {
        var weights = ScenarioCatalog.Weights(state.Setup.Duration);
        return unchecked((cash * weights.Cash
            + ControlledSectorCount(state, player.Id) * weights.ControlledSector
            + player.Support * weights.Support) / 10);
    }

    private static int ControlledSectorCount(MatchState state, PlayerId player) =>
        state.Sectors.Count(sector => sector.Owner == player);
}
