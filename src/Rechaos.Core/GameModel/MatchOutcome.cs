namespace Rechaos.Core.GameModel;

public enum MatchEndReason : byte
{
    TimeLimit,
    ObjectiveCompleted,
    PlayerEliminated
}

public sealed record MatchOutcome(
    ScenarioId Scenario,
    MatchEndReason Reason,
    int Turn,
    IReadOnlyList<PlayerId> Winners,
    IReadOnlyList<MatchStanding> Standings,
    IReadOnlyList<EndgameAwardResult> Awards);

/// <summary>
/// Projects authoritative match state into the manual-defined scenario rules.
/// End-boundary timing and simultaneous winner treatment remain provisional.
/// </summary>
public static class MatchOutcomeEvaluator
{
    private const short RightHandsDefinitionId = 0;

    public static MatchOutcome? Evaluate(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var humans = state.Players
            .Where(player => player.Setup.Controller == PlayerController.Human).ToArray();
        if (humans is [{ Status: PlayerStatus.Eliminated }])
        {
            var survivingOpponents = state.Players
                .Where(player => player.Id != humans[0].Id && player.Status == PlayerStatus.Active)
                .Select(player => player.Id)
                .OrderBy(player => player.Value)
                .ToArray();
            if (survivingOpponents.Length > 0)
                return new MatchOutcome(
                    state.Setup.Scenario,
                    MatchEndReason.PlayerEliminated,
                    state.Coordinator.Turn,
                    survivingOpponents,
                    EndgameRankingEvaluator.Evaluate(state),
                    EndgameAwardEvaluator.Evaluate(state));
        }
        var activePlayers = state.Players
            .Where(player => player.Status == PlayerStatus.Active)
            .ToArray();
        if (activePlayers.Length == 1)
        {
            return new MatchOutcome(
                state.Setup.Scenario,
                MatchEndReason.PlayerEliminated,
                state.Coordinator.Turn,
                [activePlayers[0].Id],
                EndgameRankingEvaluator.Evaluate(state),
                EndgameAwardEvaluator.Evaluate(state));
        }

        var definition = ScenarioCatalog.Get(state.Setup.Scenario);
        if (definition.IsTimed)
        {
            if (state.Coordinator.Turn < ScenarioCatalog.Turns(state.Setup.Duration)) return null;
            var standings = EndgameRankingEvaluator.Evaluate(state);
            return new MatchOutcome(
                state.Setup.Scenario,
                MatchEndReason.TimeLimit,
                state.Coordinator.Turn,
                standings.Where(standing => standing.Place == 1)
                    .Select(standing => standing.Player).ToArray(),
                standings,
                EndgameAwardEvaluator.Evaluate(state));
        }

        var winners = state.Players
            .Where(player => ScenarioCatalog.HasObjectiveVictory(
                state.Setup.Scenario, Project(state, player)))
            .Select(player => player.Id)
            .OrderBy(player => player.Value)
            .ToArray();
        return winners.Length == 0
            ? null
            : new MatchOutcome(
                state.Setup.Scenario,
                MatchEndReason.ObjectiveCompleted,
                state.Coordinator.Turn,
                winners,
                EndgameRankingEvaluator.Evaluate(state),
                EndgameAwardEvaluator.Evaluate(state));
    }

    public static PlayerScoreState Project(MatchState state, MatchPlayerState player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        if (state.FindPlayer(player.Id) != player)
            throw new ArgumentException("Player does not belong to the match.", nameof(player));

        var controlledSectors = state.Sectors.Count(sector => sector.Owner == player.Id);
        var opponents = state.Players.Where(candidate => candidate.Id != player.Id).ToArray();
        var importantSectors = state.Sectors.Count(sector =>
            sector.Owner == player.Id && sector.IsImportant);
        return new PlayerScoreState(
            player.Cash,
            player.Support,
            controlledSectors,
            player.Status == PlayerStatus.Active,
            opponents.Count(candidate => candidate.Status == PlayerStatus.Active),
            opponents.Sum(candidate => candidate.Gangs.Count(gang =>
                gang.IsActive && gang.DefinitionId == RightHandsDefinitionId)),
            importantSectors,
            player.BigManPoints);
    }
}
