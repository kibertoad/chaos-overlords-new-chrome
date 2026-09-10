using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Entering the Command phase, done the same way on every client.
/// </summary>
/// <remarks>
/// <para>
/// Drawing a player's hire offers is a mutation of shared state: it spends the deterministic PRNG.
/// In hot-seat play the game draws them lazily, when the player opens the hire dock, which is
/// harmless because there is only one state. Online it is not: a client whose player never opened
/// the dock would have spent the stream differently from one whose player did, and every later
/// draw in the match would diverge from that point on.
/// </para>
/// <para>
/// So the draw is not left to the interface. Every client refills every slot's offers at the same
/// point — the moment the turn reaches Command — human and computer alike, and none of them draws
/// again for the rest of the turn.
/// </para>
/// </remarks>
public static class CommandPhase
{
    /// <summary>
    /// Runs the automatic phases until the next Command phase, then draws every seat's offers.
    /// </summary>
    /// <remarks>
    /// This is the one way a client arrives at a Command phase, whether it has just bootstrapped
    /// the match or just resolved a turn. Both paths run the same steps in the same order, which is
    /// what makes turn 1 as reproducible across clients as turn 40.
    /// </remarks>
    public static void Enter(MatchReplayRecorder replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        Advance(replay);
        PrepareHireOffers(replay);
    }

    /// <summary>Runs Upkeep, Execution, Hire and Elimination until Command, or until the match ends.</summary>
    private static void Advance(MatchReplayRecorder replay)
    {
        var state = replay.State;
        while (state.Outcome is null && state.Coordinator.Phase != TurnPhase.Command)
        {
            switch (state.Coordinator.Phase)
            {
                case TurnPhase.Upkeep: replay.FinishUpkeep(); break;
                case TurnPhase.Execution: replay.FinishExecutionPhase(); break;
                case TurnPhase.Hire: replay.FinishHire(RequireActivePlayer(state)); break;
                case TurnPhase.PlayerElimination: replay.FinishPlayerElimination(); break;
                case TurnPhase.Command:
                default:
                    throw new InvalidOperationException(
                        $"Unexpected phase {state.Coordinator.Phase} while advancing to Command.");
            }
        }
    }

    private static PlayerId RequireActivePlayer(MatchState state) =>
        state.Coordinator.ActivePlayer
        ?? throw new InvalidOperationException(
            $"{state.Coordinator.Phase} needs an active player and has none.");

    /// <summary>
    /// Refills the hire offers of every seat, in slot order.
    /// </summary>
    /// <remarks>
    /// The condition is the game's own: a seat is refilled only when it has an empty slot, has
    /// nothing already queued, and has not snubbed this turn. Applying it here rather than in the
    /// interface is what makes it the same condition everywhere.
    /// </remarks>
    internal static void PrepareHireOffers(MatchReplayRecorder replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        var state = replay.State;
        if (state.Outcome is not null || state.Coordinator.Phase != TurnPhase.Command) return;
        for (var slot = 0; slot < state.Setup.Players.Count; slot++)
        {
            var playerId = new PlayerId(slot);
            var player = state.FindPlayer(playerId);
            if (player is null) continue;
            if (player.HireOfferSlots.Any(offer => !offer.GangDefinitionId.HasValue)
                && player.PendingHires.Count == 0
                && !player.HasSnubbedHireOfferThisTurn)
            {
                replay.PrepareHireOffers(playerId);
            }
        }
    }
}
