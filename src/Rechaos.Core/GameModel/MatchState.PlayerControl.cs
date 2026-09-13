namespace Rechaos.Core.GameModel;

public sealed partial class MatchState
{
    /// <summary>
    /// Permanently hands a human seat to the deterministic computer player at a clean turn boundary.
    /// </summary>
    /// <remarks>
    /// This is deliberately one-way and does not infer a departure from an absent order document:
    /// an absent document can also mean an active player timed out. Online callers must record an
    /// authoritative departure and apply this operation on every client before planning the seal.
    /// A sender with authenticated human Comlink history cannot transfer because current save
    /// validation cannot otherwise prove that those earlier messages were sent before the handover.
    /// </remarks>
    public bool TransferPlayerToComputer(PlayerId playerId)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (player.Setup.Controller == PlayerController.Computer) return false;
        if (Outcome is not null
            || Coordinator.Phase != TurnPhase.Command
            || Coordinator.ActivePlayer != new PlayerId(0)
            || Commands.ExecutionPlan().Count != 0
            || Players.Any(candidate => candidate.PendingHires.Count != 0))
        {
            throw new InvalidOperationException(
                "Player control can only transfer at a clean Command turn boundary.");
        }
        if (Players.Any(recipient => ComlinkFor(recipient.Id).Messages.Any(
                message => message.Sender == playerId)))
        {
            throw new InvalidOperationException(
                "A seat with authenticated human Comlink history cannot transfer to the computer.");
        }

        Setup = Setup.WithController(playerId, PlayerController.Computer);
        for (var index = 0; index < Players.Count; index++)
            Players[index].Setup = Setup.Players[index];
        return true;
    }
}
