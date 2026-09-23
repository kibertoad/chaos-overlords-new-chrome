using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
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
    private static bool CanTransferControl(MatchState state) => state.Outcome is null;

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

    /// <summary>
    /// A seat changing hands, with the turn whose resolution it took effect before.
    /// </summary>
    /// <param name="BeforeTurn">
    /// The turn the match was on when the handover was applied: the first turn the new controller
    /// plays.
    /// </param>
    private readonly record struct ControlHandover(int BeforeTurn, int Slot, PlayerController Controller);

    /// <summary>
    /// Every handover this session has applied, in log order.
    /// </summary>
    /// <remarks>
    /// A handover lives in the event log between two seals and nowhere in a sealed set, so a state
    /// rebuilt from a snapshot and sealed sets alone plays a seat taken over after the snapshot as
    /// its old controller and diverges on the next turn. The event that announced the handover is
    /// behind this client's cursor by then and is never delivered again; this is what lets a rebuild
    /// put it back at the boundary it happened on. A handful of entries over a whole match.
    /// </remarks>
    private readonly List<ControlHandover> _controlHandovers = [];

    private void TransferPlayerToComputer(string playerId) =>
        HandOverSeat(playerId, PlayerController.Computer);

    private void TransferPlayerToHuman(string playerId) =>
        HandOverSeat(playerId, PlayerController.Human);

    private void HandOverSeat(string playerId, PlayerController controller)
    {
        if (!_slotsByPlayerId.TryGetValue(playerId, out var slot)) return;
        var handover = new ControlHandover(_replay.State.Coordinator.Turn, slot, controller);
        _controlHandovers.Add(handover);
        ApplyHandover(_replay, handover);
    }

    /// <summary>
    /// Applies the handovers in <c>(afterTurn, throughTurn]</c> to a rebuilt state, in log order.
    /// </summary>
    /// <remarks>
    /// A handover that took effect before <paramref name="afterTurn"/> is already in any state
    /// rebuilt from that turn's snapshot. One at the boundary itself may or may not be, depending on
    /// when the snapshot was taken, and applying it again is a no-op; see <see cref="ApplyHandover"/>.
    /// </remarks>
    private void ApplyHandovers(MatchReplayRecorder recorder, int afterTurn, int throughTurn)
    {
        foreach (var handover in _controlHandovers)
        {
            if (handover.BeforeTurn > afterTurn && handover.BeforeTurn <= throughTurn)
                ApplyHandover(recorder, handover);
        }
    }

    /// <summary>
    /// Hands a seat over on one state, unless it is already held that way or cannot change hands.
    /// </summary>
    /// <remarks>
    /// Guarded on the state it is applied to, not on the live one: see <see cref="CanTransferControl"/>
    /// for why a finished match ignores the transfer.
    /// </remarks>
    private static void ApplyHandover(MatchReplayRecorder recorder, ControlHandover handover)
    {
        if (!CanTransferControl(recorder.State)) return;
        var player = recorder.State.FindPlayer(new PlayerId(handover.Slot));
        if (player is null || player.Setup.Controller == handover.Controller) return;
        if (handover.Controller == PlayerController.Computer)
            recorder.TransferPlayerToComputer(player.Id);
        else
            recorder.TransferPlayerToHuman(player.Id);
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
