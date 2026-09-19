using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

public sealed partial class MultiplayerMatchSession
{
    /// <summary>
    /// Whether a seat can still change hands on this state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A match that has reached its outcome has no further turn for anyone to play, so who controls
    /// a seat can no longer change any state — and the core refuses the transfer outright, because
    /// a finished match is not the clean Command boundary control transfers at. That refusal used
    /// to end the session: a player who ran out of time on the very turn that decided the match was
    /// left <c>takeoverPending</c>, the vote about them stayed on screen over the endgame, and the
    /// moment the remaining players approved computer control every client threw and replaced the
    /// endgame with a connection failure. In a two-player match that approval is one click.
    /// </para>
    /// <para>
    /// Ignoring it is safe for the lockstep. The decision is read from <see cref="MatchState.Outcome"/>,
    /// which is part of the hashed state every client has already agreed on at that point in the
    /// log, so every client — live or replaying the same events on reconnect — ignores exactly the
    /// same transfers.
    /// </para>
    /// </remarks>
    private bool CanTransferControl => _replay.State.Outcome is null;

    /// <summary>
    /// Who holds a seat, in the copy of the match this session drives.
    /// </summary>
    /// <remarks>
    /// The session hands the interface a fresh clone at each turn boundary and never shares its own
    /// — see <see cref="MatchStateClone"/> for why — so a seat changing hands, or pointedly not
    /// changing hands, between boundaries is invisible from outside. A control transfer announced
    /// after the final turn is exactly that: there is no later boundary for a clone to arrive at,
    /// which leaves this the only honest way to ask whether the transfer was ignored.
    /// </remarks>
    internal PlayerController? ControllerOfSlot(int slot) =>
        _replay.State.FindPlayer(new PlayerId(slot))?.Setup.Controller;

    private void TransferPlayerToComputer(string playerId)
    {
        if (!CanTransferControl) return;
        if (!_slotsByPlayerId.TryGetValue(playerId, out var slot)) return;
        var player = _replay.State.FindPlayer(new PlayerId(slot));
        if (player is null || player.Setup.Controller == PlayerController.Computer) return;
        _replay.TransferPlayerToComputer(player.Id);
    }

    private void TransferPlayerToHuman(string playerId)
    {
        if (!CanTransferControl) return;
        if (!_slotsByPlayerId.TryGetValue(playerId, out var slot)) return;
        var player = _replay.State.FindPlayer(new PlayerId(slot));
        if (player is null || player.Setup.Controller == PlayerController.Human) return;
        _replay.TransferPlayerToHuman(player.Id);
    }

    private void AddLatePlayer(string playerId, int slot)
    {
        if (_slotsByPlayerId.TryGetValue(playerId, out var knownSlot))
        {
            if (knownSlot != slot)
                throw new MultiplayerProtocolException("a late player changed seats");
        }
        else if (_slotsByPlayerId.Values.Contains(slot))
            throw new MultiplayerProtocolException("a late player claimed a human-owned seat");
        else
        {
            _slotsByPlayerId[playerId] = slot;
            _awaitedSlots.Add(slot);
        }
        // Through the same guarded path as any other handover: the seat is now in the map, and a
        // late join announced on a match that has already ended has no turn left to take over.
        TransferPlayerToHuman(playerId);
    }
}
