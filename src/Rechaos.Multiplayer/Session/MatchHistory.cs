using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// The state a match's event log folds to: a snapshot or a bootstrap, then the seats that changed
/// hands and the sealed sets after it, each at its place in the log.
/// </summary>
/// <remarks>
/// <para>
/// A reconnecting client and the coordination server's resolver rebuild a match the same way, so
/// both fold the log through this class: the client's session (<see cref="MultiplayerMatchSession"/>)
/// and <see cref="Resolution.AuthoritativeMatch"/>. Nothing here talks to a network. The caller
/// reads the log and answers each seal this asks for with the sealed set, which it fetches.
/// </para>
/// <para>
/// Votes, readiness, confirmations and desyncs leave the state alone and are the caller's to
/// handle; <see cref="Apply"/> ignores them.
/// </para>
/// </remarks>
public sealed class MatchHistory
{
    private readonly Dictionary<string, int> _seats;

    /// <summary>
    /// Every handover applied so far, in log order.
    /// </summary>
    /// <remarks>
    /// A handover lives in the event log between two seals and nowhere in a sealed set, so a state
    /// rebuilt from a snapshot and sealed sets alone plays a seat taken over after the snapshot as
    /// its old controller and diverges on the next turn. This is what lets a rebuild put it back at
    /// the boundary it happened on; see <see cref="ApplyHandovers"/>. A handful of entries over a
    /// whole match.
    /// </remarks>
    private readonly List<ControlHandover> _handovers = [];

    /// <param name="replay">The state the log is folded onto.</param>
    /// <param name="seats">The slot of every player id the roster seats.</param>
    /// <param name="logTurn">The turn the log is on where the fold starts; see <see cref="LogTurn"/>.</param>
    public MatchHistory(MatchReplayRecorder replay, IReadOnlyDictionary<string, int> seats, int logTurn)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(seats);
        Replay = replay;
        _seats = new Dictionary<string, int>(seats, StringComparer.Ordinal);
        LogTurn = logTurn;
    }

    /// <summary>The slot of every player the roster seats, whatever their status is now.</summary>
    /// <remarks>A player still in the lobby has slot -1 and holds no seat.</remarks>
    public static Dictionary<string, int> SeatsOf(IReadOnlyList<PlayerView> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        var seats = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var player in players)
        {
            if (MatchBootstrapFactory.IsSeated(player)) seats[player.Id] = player.Slot;
        }
        return seats;
    }

    /// <summary>The state the log has been folded to.</summary>
    public MatchReplayRecorder Replay { get; private set; }

    /// <summary>The slot of every player id the roster seats, late joins included.</summary>
    public IReadOnlyDictionary<string, int> Seats => _seats;

    /// <summary>
    /// The turn the log is on at the event being folded: one past the last turn it sealed.
    /// </summary>
    /// <remarks>
    /// A handover or a late join carries no turn, and the state is no guide to it once a snapshot
    /// has been adopted: a walk of the log from its start passes handovers the snapshot already
    /// holds. This is the turn such an event took effect before, and the one it is judged against
    /// exactly as a seal is; see <see cref="HandOverSeat"/>.
    /// </remarks>
    public int LogTurn { get; private set; }

    /// <summary>Starts a walk of the log at a point where it is on <paramref name="logTurn"/>.</summary>
    /// <remarks>
    /// Turn 1 for a walk from the log's start, and the turn the state is on for a walk from the
    /// last event the state already reflects. Read before a snapshot moves the state past it.
    /// </remarks>
    public void BeginWalk(int logTurn) => LogTurn = logTurn;

    /// <summary>Replaces the state, keeping the seats and the handovers the log has carried.</summary>
    public void Adopt(MatchReplayRecorder replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        Replay = replay;
    }

    /// <summary>
    /// Folds one event of the log.
    /// </summary>
    /// <returns>
    /// The seal the caller must answer with <see cref="ApplySealedSet"/> before the next event, or
    /// null when the event needs nothing more: it was folded, the state already holds it, or it
    /// does not change the state.
    /// </returns>
    /// <exception cref="MultiplayerProtocolException">
    /// The log seals a turn ahead of the state, or a late join contradicts the seats.
    /// </exception>
    public TurnSealedEventPayload? Apply(MatchEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        switch (@event)
        {
            case MatchPlayerTakenOverEvent takenOver:
                HandOverSeat(takenOver.Payload.PlayerId, PlayerController.Computer, LogTurn);
                return null;
            case MatchPlayerReturnedEvent { Payload.ReplacedComputer: true } returned:
                HandOverSeat(returned.Payload.PlayerId, PlayerController.Human, LogTurn);
                return null;
            case MatchLatePlayerJoinedEvent joined:
                AddLatePlayer(joined.Payload.PlayerId, joined.Payload.Slot, LogTurn);
                return null;
            case TurnOpenedEvent opened:
                LogTurn = Math.Max(LogTurn, opened.Payload.Turn);
                return null;
            case TurnSealedEvent sealedTurn:
                return Sealed(sealedTurn.Payload);
            default:
                return null;
        }
    }

    /// <summary>A seal met in the log: the turn it is for, if the state still has to apply it.</summary>
    private TurnSealedEventPayload? Sealed(TurnSealedEventPayload seal)
    {
        // Whether or not the state already holds it, the log has moved past this turn.
        LogTurn = Math.Max(LogTurn, seal.Turn + 1);
        var current = Replay.State.Coordinator.Turn;
        if (seal.Turn < current) return null;
        // A finished match has no turn left to apply. A turn can still seal on its deadline after
        // the last one, when the server cannot finish the match because a seat has not reported,
        // and the live session and a desync rebuild both pass over it, so the walk does too.
        if (Replay.State.Outcome is not null) return null;
        if (seal.Turn > current)
        {
            throw new MultiplayerProtocolException(
                $"the event history sealed turn {seal.Turn} while the reconstructed match was still "
                + $"on turn {current}");
        }
        return seal;
    }

    /// <summary>
    /// Applies the sealed set for the turn the state is on.
    /// </summary>
    /// <param name="sealedOrders">The set, as <c>GET /turns/:n/orders</c> answers it.</param>
    /// <param name="announcedOrderSetHash">The digest the log announced with the seal, when known.</param>
    /// <returns>The state hash to report.</returns>
    /// <exception cref="MultiplayerProtocolException">
    /// The set is for another turn, does not match the announced digest or its own, or carries
    /// something this build cannot apply.
    /// </exception>
    public string ApplySealedSet(SealedOrdersView sealedOrders, string? announcedOrderSetHash = null)
    {
        ArgumentNullException.ThrowIfNull(sealedOrders);
        RequireSealedSet(sealedOrders, Replay.State.Coordinator.Turn, announcedOrderSetHash);
        return SealedTurnApplier.Apply(Replay, sealedOrders);
    }

    /// <summary>
    /// Throws unless a sealed set is the one for <paramref name="turn"/>, carries the digest the
    /// event log announced when there is one, and matches its own digest.
    /// </summary>
    public static void RequireSealedSet(
        SealedOrdersView sealedOrders,
        int turn,
        string? announcedOrderSetHash = null)
    {
        ArgumentNullException.ThrowIfNull(sealedOrders);
        if (sealedOrders.Turn != turn)
        {
            throw new MultiplayerProtocolException(
                $"the server answered turn {turn}'s sealed set with the set for turn {sealedOrders.Turn}");
        }
        if (announcedOrderSetHash is not null
            && !string.Equals(sealedOrders.OrderSetHash, announcedOrderSetHash, StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                $"the sealed-set digest for turn {turn} does not match the event log");
        }
        if (!OrderDigest.Verifies(sealedOrders, sealedOrders.OrderSetHash))
        {
            throw new MultiplayerProtocolException(
                $"the sealed set for turn {turn} does not match the digest the server announced");
        }
    }

    /// <summary>
    /// Records a handover at the turn it took effect before, and applies it unless the state
    /// already reflects that turn. A player id the seats do not hold is ignored.
    /// </summary>
    /// <param name="beforeTurn">
    /// The turn the handover took effect before: the first turn the new controller plays. The log's
    /// turn for an event met in history, and the state's for one announced live.
    /// </param>
    /// <remarks>
    /// <para>
    /// The turn is the log's, not the state's. A walk from the log's start over an adopted snapshot
    /// meets every handover the match ever had while its state is already past them; the state's
    /// turn would have recorded all of them at the snapshot's boundary, and a desync repair from an
    /// older snapshot would then have put them back a turn or more late.
    /// </para>
    /// <para>
    /// The test for "already reflected" is the one a sealed turn is skipped by: a handover before
    /// turn N is part of any state standing on a turn after N. Applying such a handover again
    /// replays a seat's old controller onto the snapshot.
    /// </para>
    /// </remarks>
    public void HandOverSeat(string playerId, PlayerController controller, int beforeTurn)
    {
        ArgumentNullException.ThrowIfNull(playerId);
        if (!_seats.TryGetValue(playerId, out var slot)) return;
        var handover = new ControlHandover(beforeTurn, slot, controller);
        _handovers.Add(handover);
        if (beforeTurn < Replay.State.Coordinator.Turn) return;
        SeatControl.HandOver(Replay, handover.Slot, handover.Controller);
    }

    /// <summary>
    /// A player joining an AI seat after the start, at the turn the join took effect before.
    /// </summary>
    /// <returns>Whether the player was not seated before.</returns>
    /// <exception cref="MultiplayerProtocolException">
    /// The player is seated elsewhere, or the slot belongs to another player the computer does not
    /// play.
    /// </exception>
    /// <remarks>
    /// A late joiner may take a seat that a human held until the vote handed it to the computer, so
    /// a seat this history already knows a player for is not by itself a contradiction. One the
    /// computer does not play at the point of the join is: the server has seated a second human in
    /// a chair somebody is still playing. That can be judged only while the state stands at that
    /// point; a replay over a later snapshot has already settled who plays it.
    /// </remarks>
    public bool AddLatePlayer(string playerId, int slot, int beforeTurn)
    {
        ArgumentNullException.ThrowIfNull(playerId);
        var added = false;
        if (_seats.TryGetValue(playerId, out var knownSlot))
        {
            if (knownSlot != slot)
                throw new MultiplayerProtocolException("a late player changed seats");
        }
        else if (_seats.ContainsValue(slot)
                 && beforeTurn >= Replay.State.Coordinator.Turn
                 && SeatControl.CanTransfer(Replay.State)
                 && Replay.State.FindPlayer(new PlayerId(slot))?.Setup.Controller != PlayerController.Computer)
            throw new MultiplayerProtocolException("a late player claimed a human-owned seat");
        else
        {
            _seats[playerId] = slot;
            added = true;
        }
        // Through the same guarded path as any other handover: the seat is now in the map, and a
        // late join announced on a match that has already ended has no turn left to take over.
        HandOverSeat(playerId, PlayerController.Human, beforeTurn);
        return added;
    }

    /// <summary>
    /// Applies the handovers in <c>(afterTurn, throughTurn]</c> to a rebuilt state, in log order.
    /// </summary>
    /// <remarks>
    /// A handover that took effect before <paramref name="afterTurn"/> is already in any state
    /// rebuilt from that turn's snapshot. One at the boundary itself may or may not be, depending on
    /// when the snapshot was taken, and applying it again does nothing; see
    /// <see cref="SeatControl.HandOver"/>.
    /// </remarks>
    public void ApplyHandovers(MatchReplayRecorder recorder, int afterTurn, int throughTurn)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        foreach (var handover in _handovers)
        {
            if (handover.BeforeTurn > afterTurn && handover.BeforeTurn <= throughTurn)
                SeatControl.HandOver(recorder, handover.Slot, handover.Controller);
        }
    }

    /// <summary>
    /// A seat changing hands, with the turn whose resolution it took effect before.
    /// </summary>
    /// <param name="BeforeTurn">
    /// The turn the match was on when the handover was applied: the first turn the new controller
    /// plays.
    /// </param>
    private readonly record struct ControlHandover(int BeforeTurn, int Slot, PlayerController Controller);
}
