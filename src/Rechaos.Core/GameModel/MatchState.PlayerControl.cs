namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    /// <summary>
    /// Permanently hands a human seat to the deterministic computer player at a clean turn boundary.
    /// </summary>
    /// <remarks>
    /// This is deliberately one-way and does not infer a departure from an absent order document:
    /// an absent document can also mean an active player timed out. Online callers must record an
    /// authoritative approved-takeover event and apply this operation on every client before planning the seal.
    /// A sender with authenticated human Comlink history cannot transfer because current save
    /// validation cannot otherwise prove that those earlier messages were sent before the handover.
    /// </remarks>
    public bool TransferPlayerToComputer(PlayerId playerId)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (player.Setup.Controller == PlayerController.Computer) return false;
        RequireCleanCommandBoundary();
        if (Players.Any(recipient => ComlinkFor(recipient.Id).Messages.Any(
                message => message.Sender == playerId)))
        {
            throw new InvalidOperationException(
                "A seat with authenticated human Comlink history cannot transfer to the computer.");
        }

        // The seat's carried-over recurring orders go with the human. The AI planner never writes
        // or clears Repeat, so a gang the planner leaves idle would otherwise keep running the
        // departed player's Heal or Influence for the rest of the match.
        foreach (var gang in player.Gangs.Where(gang => Commands.TryGet(gang.Id, out _)).ToArray())
            CancelQueuedCommand(gang);

        SetController(playerId, PlayerController.Computer);
        // RULE-AI-001, RULE-AI-027: the computer that takes over a seat plans every gang as a
        // raider.
        AiPlanning.SetRaiderMode(playerId, true);
        return true;
    }

    /// <summary>Returns a computer-controlled online seat to its authenticated human owner.</summary>
    public bool TransferPlayerToHuman(PlayerId playerId)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (player.Setup.Controller == PlayerController.Human) return false;
        RequireCleanCommandBoundary();
        SetController(playerId, PlayerController.Human);
        AiPlanning.SetRaiderMode(playerId, false);
        return true;
    }

    private void SetController(PlayerId playerId, PlayerController controller)
    {
        Setup = Setup.WithController(playerId, controller);
        for (var index = 0; index < Players.Count; index++)
            Players[index].Setup = Setup.Players[index];
    }

    /// <summary>
    /// Refuses a transfer once anyone has started planning the open turn.
    /// </summary>
    /// <remarks>
    /// The queue itself cannot answer this question: <see cref="TurnCommandQueue.FinishExecution"/>
    /// carries every command with <c>Repeat</c> set into the next Command phase, and Repeat travels
    /// in the online order document, so from the first recurring Research or Influence onward the
    /// queue is never empty at a turn boundary. Ask the event log instead, which records the moment
    /// an order was issued. <see cref="GameEventKind.HireOfferRefilled"/> is excluded because the
    /// simultaneous hire draw writes it without anybody planning.
    /// </remarks>
    private void RequireCleanCommandBoundary()
    {
        if (Outcome is not null
            || Coordinator.Phase != TurnPhase.Command
            || Coordinator.ActivePlayer != new PlayerId(0)
            || Players.Any(candidate => candidate.PendingHires.Count != 0)
            || HasPlanningActivityThisTurn())
        {
            throw new InvalidOperationException(
                "Player control can only transfer at a clean Command turn boundary.");
        }
    }

    private bool HasPlanningActivityThisTurn()
    {
        // Events are append-only in turn order, so the open turn's tail is enough.
        for (var index = _events.Count - 1; index >= 0; index--)
        {
            var gameEvent = _events[index];
            if (gameEvent.Turn != Coordinator.Turn) return false;
            if (gameEvent.Phase != TurnPhase.Command) continue;
            if (gameEvent.Kind is GameEventKind.CommandQueued or GameEventKind.CommandReplaced
                or GameEventKind.CommandCancelled or GameEventKind.HireQueued
                or GameEventKind.HireOfferSnubbed)
                return true;
        }
        return false;
    }
}
