using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// A seat changing hands between a human and the computer, applied the same way by every party
/// that holds the match: each client, and a resolver that holds it on the server.
/// </summary>
public static class SeatControl
{
    /// <summary>
    /// Whether a seat can still change hands on this state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A match that has reached its outcome has no further turn for anyone to play, so who controls
    /// a seat can no longer change any state, and the core refuses the transfer outright, because
    /// a finished match is not the clean Command boundary control transfers at. That refusal used
    /// to end the session: a player who ran out of time on the very turn that decided the match was
    /// left <c>takeoverPending</c>, the vote about them stayed on screen over the endgame, and the
    /// moment the remaining players approved computer control every client threw and replaced the
    /// endgame with a connection failure. In a two-player match that approval is one click.
    /// </para>
    /// <para>
    /// Ignoring it is safe for the lockstep. The decision is read from <see cref="MatchState.Outcome"/>,
    /// which is part of the hashed state every client has already agreed on at that point in the
    /// log, so every client, live or replaying the same events on reconnect, ignores exactly the
    /// same transfers.
    /// </para>
    /// </remarks>
    public static bool CanTransfer(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Outcome is null;
    }

    /// <summary>
    /// Hands a seat over on one state, unless it is already held that way or cannot change hands.
    /// </summary>
    /// <remarks>
    /// Guarded on the state it is applied to, not on a live one: see <see cref="CanTransfer"/> for
    /// why a finished match ignores the transfer.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The slot is past the board.</exception>
    public static void HandOver(MatchReplayRecorder recorder, int slot, PlayerController controller)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        if (!CanTransfer(recorder.State)) return;
        var player = recorder.State.FindPlayer(new PlayerId(slot));
        if (player is null || player.Setup.Controller == controller) return;
        if (controller == PlayerController.Computer)
            recorder.TransferPlayerToComputer(player.Id);
        else
            recorder.TransferPlayerToHuman(player.Id);
    }
}
