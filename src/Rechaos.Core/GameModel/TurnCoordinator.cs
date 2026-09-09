namespace Rechaos.Core.GameModel;

public sealed record TurnTransition(
    int PreviousTurn,
    TurnPhase PreviousPhase,
    ExecutionPhase? PreviousExecutionPhase,
    int Turn,
    TurnPhase Phase,
    ExecutionPhase? ExecutionPhase,
    PlayerId? ActivePlayer);

/// <summary>
/// Headless phase state machine based on the manual-documented phase order.
/// Player order and within-phase tie breaking remain provisional pending binary validation.
/// </summary>
public sealed class TurnCoordinator
{
    private readonly int _playerCount;

    public TurnCoordinator(int playerCount = MatchLimits.PlayerCount)
    {
        if (playerCount is < 1 or > MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        _playerCount = playerCount;
    }

    public int Turn { get; private set; } = 1;
    public TurnPhase Phase { get; private set; } = TurnPhase.Upkeep;
    public ExecutionPhase? ExecutionPhase { get; private set; }
    public PlayerId? ActivePlayer { get; private set; }

    public TurnTransition FinishUpkeep()
    {
        Require(TurnPhase.Upkeep);
        return Change(TurnPhase.Command, activePlayer: new PlayerId(0));
    }

    public TurnTransition FinishCommand(PlayerId player)
    {
        RequireActive(TurnPhase.Command, player);
        return player.Value + 1 < _playerCount
            ? Change(TurnPhase.Command, activePlayer: new PlayerId(player.Value + 1))
            : Change(TurnPhase.Execution, global::Rechaos.Core.GameModel.ExecutionPhase.Instant);
    }

    public TurnTransition FinishExecutionPhase()
    {
        Require(TurnPhase.Execution);
        var current = ExecutionPhase ?? throw new InvalidOperationException("Execution subphase is missing.");
        var index = TurnStructure.ExecutionIndex(current);
        return index + 1 < TurnStructure.ExecutionOrder.Count
            ? Change(TurnPhase.Execution, TurnStructure.ExecutionOrder[index + 1])
            : Change(TurnPhase.Hire, activePlayer: new PlayerId(0));
    }

    public TurnTransition FinishHire(PlayerId player)
    {
        RequireActive(TurnPhase.Hire, player);
        return player.Value + 1 < _playerCount
            ? Change(TurnPhase.Hire, activePlayer: new PlayerId(player.Value + 1))
            : Change(TurnPhase.PlayerElimination);
    }

    public TurnTransition FinishPlayerElimination()
    {
        Require(TurnPhase.PlayerElimination);
        var transition = Change(TurnPhase.Upkeep);
        Turn++;
        return transition with { Turn = Turn };
    }

    private TurnTransition Change(
        TurnPhase phase,
        ExecutionPhase? executionPhase = null,
        PlayerId? activePlayer = null)
    {
        var previousTurn = Turn;
        var previousPhase = Phase;
        var previousExecutionPhase = ExecutionPhase;
        Phase = phase;
        ExecutionPhase = executionPhase;
        ActivePlayer = activePlayer;
        return new TurnTransition(previousTurn, previousPhase, previousExecutionPhase,
            Turn, Phase, ExecutionPhase, ActivePlayer);
    }

    private void Require(TurnPhase phase)
    {
        if (Phase != phase)
            throw new InvalidOperationException($"Cannot complete {phase} while in {Phase}.");
    }

    private void RequireActive(TurnPhase phase, PlayerId player)
    {
        Require(phase);
        if (ActivePlayer != player)
            throw new InvalidOperationException($"Player {player} is not active; expected {ActivePlayer}.");
    }
}
