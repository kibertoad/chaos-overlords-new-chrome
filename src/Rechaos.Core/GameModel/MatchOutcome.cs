namespace Rechaos.Core.GameModel;

public enum MatchEndReason : byte
{
    TimeLimit,
    ObjectiveCompleted,
    PlayerEliminated,
    /// <summary>
    /// RULE-OBJECTIVE-005: every human was eliminated in an earlier turn, so the round that would
    /// come next finds no human to plan and the game returns to the title without the awards.
    /// </summary>
    NoHumansLeft
}

public sealed record MatchOutcome(
    ScenarioId Scenario,
    MatchEndReason Reason,
    int Turn,
    IReadOnlyList<PlayerId> Winners,
    IReadOnlyList<MatchStanding> Standings,
    IReadOnlyList<EndgameAwardResult> Awards);

internal static class MatchOutcomeValidator
{
    public static MatchOutcome Freeze(MatchOutcome value) => value with
    {
        Winners = Array.AsReadOnly(value.Winners.ToArray()),
        Standings = Array.AsReadOnly(value.Standings.ToArray()),
        Awards = Array.AsReadOnly(value.Awards.Select(award => award with
        {
            Recipients = Array.AsReadOnly(award.Recipients.ToArray())
        }).ToArray())
    };

    public static bool IsValid(
        MatchOutcome value,
        MatchSetup setup,
        int currentTurn)
    {
        var players = setup.Players.Select(player => player.Id).ToHashSet();
        return value.Scenario == setup.Scenario
            && Enum.IsDefined(value.Reason)
            && value.Turn is >= 1 && value.Turn <= currentTurn
            && value.Winners.Count == value.Winners.Distinct().Count()
            && value.Winners.All(players.Contains)
            && value.Standings.Count == players.Count
            && value.Standings.Select(standing => standing.Player).ToHashSet().SetEquals(players)
            // Place 0 is the ranking's own word for unranked, which is what every eliminated player
            // gets, and an empty winner list is a match everybody lost. Demanding a place for each
            // and a winner for the match is what used to make every finished match with an
            // eliminated player in it refuse to load.
            && value.Standings.All(standing => standing.Place is >= 0
                && standing.Place <= players.Count)
            && value.Awards.Select(award => award.Award).Distinct().Count() == value.Awards.Count
            && value.Awards.All(award => Enum.IsDefined(award.Award)
                && award.Recipients.Count > 0
                && award.Recipients.Count == award.Recipients.Distinct().Count()
                && award.Recipients.All(players.Contains));
    }

    public static bool Matches(MatchOutcome outcome, MatchOutcomeDetails details) =>
        outcome.Scenario == details.Scenario
        && outcome.Reason == details.Reason
        && outcome.Turn == details.CompletedTurn
        && outcome.Winners.SequenceEqual(details.Winners)
        && outcome.Standings.SequenceEqual(details.Standings)
        && outcome.Awards.Count == details.Awards.Count
        && outcome.Awards.Zip(details.Awards).All(pair =>
            pair.First.Award == pair.Second.Award
            && pair.First.Value == pair.Second.Value
            && pair.First.Recipients.SequenceEqual(pair.Second.Recipients));
}

/// <summary>
/// Projects authoritative match state into the recovered scenario rules after
/// player elimination and Big Man accrual. Objective qualifiers retain every
/// active simultaneous winner; a sole active Overlord takes precedence.
/// </summary>
public static class MatchOutcomeEvaluator
{
    private const short RightHandsDefinitionId = 0;

    /// <param name="humanActiveAtTurnStart">
    /// Whether a human was still playing before this turn's eliminations. When none was and none is
    /// now, and nothing else ends the match, the match ends as <see cref="MatchEndReason.NoHumansLeft"/>
    /// (RULE-OBJECTIVE-005).
    /// </param>
    public static MatchOutcome? Evaluate(MatchState state, bool humanActiveAtTurnStart = true) =>
        EvaluateEnd(state) ?? (humanActiveAtTurnStart || !AllHumansEliminated(state)
            ? null
            : Conclude(state, MatchEndReason.NoHumansLeft, []));

    private static bool AllHumansEliminated(MatchState state)
    {
        var humans = state.Players
            .Where(player => player.Setup.Controller == PlayerController.Human).ToArray();
        return humans.Length > 0 && humans.All(player => player.Status == PlayerStatus.Eliminated);
    }

    // RULE-OBJECTIVE-001: one player left ends every scenario, and the scenario's own test
    // (RULE-OBJECTIVE-004) runs after it.
    private static MatchOutcome? EvaluateEnd(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var activePlayers = state.Players
            .Where(player => player.Status == PlayerStatus.Active)
            .ToArray();
        if (activePlayers.Length == 1)
            return Conclude(state, MatchEndReason.PlayerEliminated, [activePlayers[0].Id]);
        // Nobody left: the last two Right Hands can destroy each other in one Combat pass, and both
        // owners are eliminated together. The match is over and no one won it. Without this the
        // evaluator answered null and the board sat there for good, or the time limit produced an
        // outcome with no winner in it and the match-ended event threw on the empty list.
        if (activePlayers.Length == 0)
            return Conclude(state, MatchEndReason.PlayerEliminated, []);

        var definition = ScenarioCatalog.Get(state.Setup.Scenario);
        if (definition.IsTimed)
        {
            // RULE-OBJECTIVE-004: the match ends with the resolution of the turn numbered with the
            // limit. The turn only counts up and the match stops there, so the test is an equality.
            if (state.Coordinator.Turn != ScenarioCatalog.Turns(state.Setup.Duration)) return null;
            var standings = EndgameRankingEvaluator.Evaluate(state);
            return Conclude(
                state,
                MatchEndReason.TimeLimit,
                standings.Where(standing => standing.Place == 1)
                    .Select(standing => standing.Player).ToArray(),
                standings);
        }

        var winners = state.Players
            .Where(player => ScenarioCatalog.HasObjectiveVictory(
                state.Setup.Scenario, Project(state, player)))
            .Select(player => player.Id)
            .OrderBy(player => player.Value)
            .ToArray();
        return winners.Length == 0
            ? null
            : Conclude(state, MatchEndReason.ObjectiveCompleted, winners);
    }

    /// <summary>
    /// The outcome for this turn: the winners as given, then the standings (evaluated here unless
    /// the caller already ranked the players to pick the winners), then the awards.
    /// </summary>
    private static MatchOutcome Conclude(
        MatchState state,
        MatchEndReason reason,
        IReadOnlyList<PlayerId> winners,
        IReadOnlyList<MatchStanding>? standings = null) => new(
        state.Setup.Scenario,
        reason,
        state.Coordinator.Turn,
        winners,
        standings ?? EndgameRankingEvaluator.Evaluate(state),
        EndgameAwardEvaluator.Evaluate(state));

    public static PlayerScoreState Project(MatchState state, MatchPlayerState player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        state.RequirePlayer(player);

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
