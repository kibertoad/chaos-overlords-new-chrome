using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Which seats have finished the open turn: kept from the log and the match view, and said to the
/// interface, which counts them under the portraits.
/// </summary>
public sealed partial class MultiplayerMatchSession
{
    /// <summary>
    /// Says which of the awaited seats have finished the turn being planned.
    /// </summary>
    /// <remarks>
    /// Readiness belongs to one turn: a roster kept for an earlier one says nothing about this one,
    /// so it reports nobody ready rather than carrying the old one forward.
    /// </remarks>
    private void PublishReadiness()
    {
        var turn = Replay.State.Coordinator.Turn;
        _notices.Enqueue(new MultiplayerNotice.ReadinessChanged(
            turn, _readinessTurn == turn ? ReadySlots() : [], AwaitedSlots()));
    }

    /// <summary>The seats of the players that have said they are done with the open turn.</summary>
    private HashSet<int> ReadySlots()
    {
        var slots = new HashSet<int>();
        foreach (var playerId in _readyPlayerIds)
        {
            if (_history.Seats.TryGetValue(playerId, out var slot)) slots.Add(slot);
        }
        return slots;
    }

    /// <summary>
    /// A copy of the awaited seats, because a notice outlives the roster it was made from.
    /// </summary>
    /// <remarks>
    /// The pump replaces the set whenever the roster changes, and the game thread reads notices
    /// whenever it next draws; handing out the live set would let a frame see a roster from after
    /// the tally it is drawn beside.
    /// </remarks>
    private HashSet<int> AwaitedSlots()
    {
        var slots = new HashSet<int>(_awaitedSlots);
        foreach (var playerId in _takeoverVotes.Keys)
        {
            if (_departedPlayerIds.Contains(playerId)
                && _history.Seats.TryGetValue(playerId, out var slot))
                slots.Add(slot);
        }
        return slots;
    }

    /// <summary>
    /// Keeps the roster of seats that have said they are done with the open turn.
    /// </summary>
    /// <remarks>
    /// A turn's readiness is forgotten when a later turn's arrives, so it never carries over. Only
    /// seats held by a human player are kept, which is the roster the server waits on.
    /// </remarks>
    private void NoteReadiness(int turn, string playerId, bool ready)
    {
        if (!RecordReadiness(turn, playerId, ready)) return;
        // For the turn the event names, not the one this client has replayed to: the server can be
        // a turn ahead of a client that is still applying the seal before it.
        _notices.Enqueue(
            new MultiplayerNotice.ReadinessChanged(turn, ReadySlots(), AwaitedSlots()));
    }

    /// <summary>
    /// Keeps one seat's readiness without telling the interface, which a history replay does for
    /// every event and says once at the end.
    /// </summary>
    /// <returns>False when the player holds no seat, so there is nothing to keep.</returns>
    private bool RecordReadiness(int turn, string playerId, bool ready)
    {
        if (!_history.Seats.ContainsKey(playerId)) return false;
        if (turn != _readinessTurn)
        {
            _readinessTurn = turn;
            _readyPlayerIds.Clear();
        }
        if (ready) _readyPlayerIds.Add(playerId);
        else _readyPlayerIds.Remove(playerId);
        return true;
    }

    /// <summary>
    /// Takes the open turn's readiness from a match view, replacing whatever was kept.
    /// </summary>
    /// <remarks>
    /// The view is the only place a client that was not listening can learn it. The live
    /// <c>turn.readiness</c> events for a turn a player marked done before this client started, or
    /// while it was away, are behind the sequence its stream resumes after and are never delivered
    /// again, so a reconnecting client showed "READY 0/N" over a turn half the table had finished
    /// until somebody happened to toggle.
    /// </remarks>
    /// <returns>Whether any seat is ready, which is when there is anything worth saying.</returns>
    private bool SeedReadiness(MatchView view)
    {
        if (view.Turn is not { } turn || turn.Number != view.CurrentTurn) return false;
        _readinessTurn = turn.Number;
        _readyPlayerIds.Clear();
        foreach (var playerId in turn.ReadyPlayerIds)
        {
            if (_history.Seats.ContainsKey(playerId)) _readyPlayerIds.Add(playerId);
        }
        return _readyPlayerIds.Count > 0;
    }
}
