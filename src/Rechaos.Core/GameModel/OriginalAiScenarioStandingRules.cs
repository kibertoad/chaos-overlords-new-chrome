namespace Rechaos.Core.GameModel;

/// <summary>
/// Scenario scores and zero-based competition standings rebuilt by the original
/// routine at 0x0047712a before AI planning.
/// </summary>
internal static class OriginalAiScenarioStandingRules
{
    public const int InactiveStanding = byte.MaxValue;
    internal const int InactiveScore = -32_000;

    public static IReadOnlyList<int> Build(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var scores = Enumerable.Repeat(InactiveScore, MatchLimits.PlayerCount).ToArray();
        foreach (var player in state.Players.Where(player => player.Status == PlayerStatus.Active))
            scores[player.Id.Value] = Score(state, player);

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
        state.Setup.Scenario switch
        {
            ScenarioId.Greed => player.Cash,
            ScenarioId.Power or ScenarioId.Big40 or ScenarioId.Armageddon =>
                ControlledSectorCount(state, player.Id),
            ScenarioId.Acceptance => player.Support,
            ScenarioId.Dominance => DominanceScore(state, player),
            ScenarioId.KillEmAll or ScenarioId.Siege =>
                MatchLimits.PlayerCount - state.Players.Count(candidate =>
                    candidate.Status == PlayerStatus.Active),
            ScenarioId.Eliminate => OriginalCityGenerator.HeadquartersCandidates.Count(
                sectorId => state.Sectors[sectorId].Owner == player.Id),
            ScenarioId.BigMan => new[] { 27, 28, 35, 36 }.Count(
                sectorId => state.Sectors[sectorId].Owner == player.Id),
            _ => throw new ArgumentOutOfRangeException()
        };

    private static int DominanceScore(MatchState state, MatchPlayerState player)
    {
        var weights = ScenarioCatalog.Weights(state.Setup.Duration);
        return unchecked((player.Cash * weights.Cash
            + ControlledSectorCount(state, player.Id) * weights.ControlledSector
            + player.Support * weights.Support) / 10);
    }

    private static int ControlledSectorCount(MatchState state, PlayerId player) =>
        state.Sectors.Count(sector => sector.Owner == player);
}
