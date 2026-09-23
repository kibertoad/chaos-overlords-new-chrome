using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Reconciling this client with a match that has moved on without it: at startup, and again
/// whenever the live stream proves to have skipped something.
/// </summary>
public sealed partial class MultiplayerMatchSession
{
    private const int EventHistoryPageSize = 200;

    /// <summary>
    /// Sealed sets fetched ahead of the turn that needs them, while earlier turns are being applied.
    /// </summary>
    /// <remarks>
    /// Replay is sequential by nature — turn N has to be applied before N+1 — but FETCHING is not:
    /// a sealed set is immutable, served with <c>Cache-Control: immutable</c>, and its contents do
    /// not depend on anything the replay does. Every set used to be fetched only when its turn came
    /// round, so a reconnect that had to replay ten turns spent ten round trips end to end with the
    /// player watching. Asking for the next few while the current one resolves overlaps the network
    /// with the work.
    /// </remarks>
    private const int SealedSetPrefetchDepth = 4;

    private readonly Dictionary<int, Task<SealedOrdersView>> _prefetchedSealedSets = [];

    /// <summary>
    /// Every turn reconstructed from history whose confirmation the log has not carried, in turn
    /// order, with the report captured when it was applied, so that the reports a crash interrupted
    /// are made after all.
    /// </summary>
    /// <remarks>
    /// A list rather than one slot. A client can miss more than one seal — a laptop that sleeps
    /// through a timed turn seals on the draft it already sent, and the next one seals absent — and
    /// each replayed seal used to overwrite the last. The server waits for a report from every human
    /// seat, so the forgotten turn stayed `sealed` for the rest of the match, `listUnsettled` never
    /// emptied, and `resumeAfterDesync` could therefore never lift a later desync pause.
    /// </remarks>
    private readonly List<TurnReport> _unreportedSeals = [];

    /// <summary>
    /// Fetches the match, adopts the newest snapshot the local state is behind, replays the log
    /// gaplessly to the view's sequence, and hands the interface the result.
    /// </summary>
    /// <param name="replayFromSeq">
    /// The sequence the local state is already consistent with: 0 for a freshly generated match,
    /// or the last applied sequence when the live stream is being resynchronised.
    /// </param>
    /// <returns>False when the match is over and there is nothing left to pump.</returns>
    private async Task<bool> RestoreAsync(int replayFromSeq, CancellationToken cancellationToken)
    {
        if (!await RebuildFromHistoryAsync(replayFromSeq, cancellationToken).ConfigureAwait(false))
            return false;
        await ResolvePendingDesyncAsync(lookForAPostedRepair: true, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Everything <see cref="RestoreAsync"/> does up to and including handing the interface the
    /// result, without the desync resolution that follows it.
    /// </summary>
    /// <remarks>
    /// Separate so that a caller retrying a restore whose last step ran out of its retry window
    /// can retry only that step. Re-running the whole rebuild after <c>Resumed</c> has gone out
    /// announces the match a second time, minutes later, over whatever the player planned since.
    /// </remarks>
    /// <returns>False when the match is over and there is nothing left to pump.</returns>
    private async Task<bool> RebuildFromHistoryAsync(int replayFromSeq, CancellationToken cancellationToken)
    {
        var (view, snapshot) = await ReadViewAndLatestSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (view.Status == MatchStatus.Abandoned)
        {
            _notices.Enqueue(new MultiplayerNotice.MatchAbandoned());
            return false;
        }
        if (view.CurrentTurn < 1)
        {
            throw new MultiplayerProtocolException(
                $"a started match cannot be on turn {view.CurrentTurn}");
        }

        // Turn 1 included. The host uploads a bootstrap snapshot for turn 0, and that row is the
        // recovery baseline until a desync requires a newer one — so a client that skipped it on
        // turn 1 either refused the only snapshot there was, or bootstrapped a city of its own
        // next to everyone else's. A snapshot older than the local state has nothing to add.
        if (snapshot is not null && snapshot.Turn + 1 >= _replay.State.Coordinator.Turn)
            AdoptResumeSnapshot(snapshot, view.CurrentTurn);
        // A host that crashed before its bootstrap upload completed never tried again, because the
        // flag that drives it is only set for a session that is NOT restoring — and the server then
        // answered `late_join_not_ready` to every late join for the life of the match.
        _uploadInitialSnapshot |= IsHost && snapshot is null && view.CurrentTurn == 1;

        await ReplayEventHistoryAsync(replayFromSeq, view.LastEventSeq, cancellationToken)
            .ConfigureAwait(false);

        if (_replay.State.Outcome is null
            && (_replay.State.Coordinator.Phase != TurnPhase.Command
                || _replay.State.Coordinator.Turn != view.CurrentTurn))
        {
            throw new MultiplayerProtocolException(
                $"the resumed state reached {_replay.State.Coordinator.Phase} turn "
                + $"{_replay.State.Coordinator.Turn}, but the server is on turn {view.CurrentTurn}");
        }
        // The pending reports go out BEFORE the "ended but the server says running" check, not
        // after it. The server marks a match finished only once every human seat has reported the
        // final turn, so a client that replayed that seal from history is the reason the match is
        // still running — and refusing it here left the player locked out for good and the server
        // match unfinishable.
        if (_unreportedSeals.Count > 0)
        {
            // In turn order. The turn a report is missing from is the turn the barrier is waiting
            // on, and an earlier one left unsettled blocks every later desync repair.
            // All queued at once: the reporter holds them in that order anyway, so waiting for
            // each answer before queuing the next only added a round trip per turn.
            var reports = _unreportedSeals.OrderBy(item => item.Turn).Select(QueueReportAsync).ToArray();
            await Task.WhenAll(reports).WaitAsync(cancellationToken).ConfigureAwait(false);
            _unreportedSeals.Clear();
            // The reports may have finished the match; re-read rather than judge it on the view
            // that was fetched before they were sent.
            view = await ReadMatchViewAsync(cancellationToken).ConfigureAwait(false);
        }
        if (_replay.State.Outcome is not null
            && view.Status is not (MatchStatus.Finished or MatchStatus.Abandoned))
        {
            throw new MultiplayerProtocolException(
                "the reconstructed match has ended while the server still reports it in progress");
        }

        var submission = _replay.State.Outcome is null
            ? await CallAsync(
                token => _match.OwnSubmissionAsync(view.CurrentTurn, token),
                _pumpLane,
                cancellationToken).ConfigureAwait(false)
            : new OwnSubmissionView(view.CurrentTurn, null, Ready: false, OrdersHash: null);
        ValidateResumeSubmission(submission, view.CurrentTurn);
        // Two copies leave here and no more: the interface's own authoritative copy, and the
        // planning copy built over it. Building the planning copy is also what proves the saved
        // draft still applies to this state — a draft that does not is refused here, on this
        // thread, as a failure of the protocol rather than as a crash on the game thread.
        var state = MatchStateClone.Of(_replay.State, _definitions);
        var turn = state.Outcome is null
            ? submission.Orders is { } document
                ? SpeculativeTurn.Restore(state, _definitions, Slot, document)
                : SpeculativeTurn.For(state, _definitions, Slot)
            : null;
        _resumeAfterSeq = view.LastEventSeq;
        _notices.Enqueue(new MultiplayerNotice.Resumed(view, state, submission, turn));
        foreach (var vote in _takeoverVotes.Values.OrderBy(item => item.PlayerId, StringComparer.Ordinal))
            PublishTakeoverVote(vote);
        // The interface counts the seats from the roster alone, which cannot see a departed seat the
        // server is still holding the turn for while the vote on it is open.
        if (_takeoverVotes.Keys.Any(_departedPlayerIds.Contains)) PublishReadiness();
        // Last, on a state that is now caught up, the caller resolves a pause the history carried
        // — adopt a repair somebody posted while this client was away, or post one if this client
        // turns out to be holding the state the others agreed on. Until that existed, every
        // restart during a desync simply rejoined the wait.
        return true;
    }

    /// <summary>
    /// The match view and the newest snapshot, read so that the view is never behind the snapshot.
    /// </summary>
    /// <remarks>
    /// Two reads, and a turn can resolve between them: the view says turn N and the snapshot
    /// that lands next is for turn N, which no state on turn N can be restored from. The view is
    /// the authority, so when that happens the view is read again rather than the session ended.
    /// </remarks>
    private async Task<(MatchView View, SnapshotView? Snapshot)> ReadViewAndLatestSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var view = await ReadMatchViewAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = await LatestSnapshotOrNullAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot is not null && snapshot.Turn >= view.CurrentTurn)
            view = await ReadMatchViewAsync(cancellationToken).ConfigureAwait(false);
        return (view, snapshot);
    }

    private async Task<SnapshotView?> LatestSnapshotOrNullAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await CallAsync(
                token => _match.LatestSnapshotAsync(token), _pumpLane, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MultiplayerApiException exception) when (exception.Reason == "no_snapshot")
        {
            return null;
        }
    }

    /// <remarks>
    /// Turn 0 is the host's bootstrap snapshot, taken before a single turn was played. It restores
    /// like any other — the state it holds is the one every client generates for turn 1 — and it is
    /// the baseline every ordinary reconnect replays the server's sealed order sets from.
    /// </remarks>
    private void AdoptResumeSnapshot(SnapshotView snapshot, int currentTurn)
    {
        if (snapshot.Turn < 0 || snapshot.Turn >= currentTurn)
        {
            throw new MultiplayerProtocolException(
                $"the latest snapshot is for turn {snapshot.Turn}, but the server is on turn "
                + currentTurn);
        }
        var restored = ReadVerifiedSnapshot(snapshot);
        if (restored.Outcome is null
            && (restored.Coordinator.Phase != TurnPhase.Command
                || restored.Coordinator.Turn != snapshot.Turn + 1))
        {
            throw new MultiplayerProtocolException(
                $"the snapshot for turn {snapshot.Turn} resumes at "
                + $"{restored.Coordinator.Phase} turn {restored.Coordinator.Turn}");
        }
        _replay = new MatchReplayRecorder(restored);
        _canonicalThroughTurn = snapshot.Turn;
    }

    /// <summary>
    /// Reconstructs controller handovers and sealed turns in their authoritative log order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vote requests and choices never change the deterministic model. Replaying only the approved
    /// takeover fact preserves the exact boundary between a human-held idle turn and the first turn
    /// on which every client may computer-plan that seat.
    /// </para>
    /// <para>
    /// The pages are cheap next to what they lead to: a seal for a turn the state already holds
    /// costs nothing but its line in the page, and only the seals past it fetch a sealed set. The
    /// events before a snapshot's turn are still read because a takeover vote opened before it can
    /// still be open, and its tally lives nowhere but in the log.
    /// </para>
    /// </remarks>
    private async Task ReplayEventHistoryAsync(int fromSeq, int throughSeq, CancellationToken cancellationToken)
    {
        var after = fromSeq;
        try
        {
            await ReplayPagesAsync(after, throughSeq, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Whatever is still in flight belongs to a replay that is over, one way or the other.
            foreach (var pending in _prefetchedSealedSets.Values) Forget(pending);
            _prefetchedSealedSets.Clear();
        }
    }

    /// <summary>Swallows the result of a task nothing is going to read.</summary>
    /// <remarks>
    /// A prefetch a replay abandoned, and the queued state-hash report the pump hands off so
    /// that a resolved turn reaches the player without waiting for the round trip.
    /// </remarks>
    private static void Forget(Task task) =>
        _ = task.ContinueWith(
            static finished => _ = finished.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private async Task ReplayPagesAsync(
        int after,
        int throughSeq,
        CancellationToken cancellationToken)
    {
        while (after < throughSeq)
        {
            var page = await CallAsync(
                token => _match.EventsAsync(after, EventHistoryPageSize, token),
                _pumpLane,
                cancellationToken).ConfigureAwait(false);
            if (page.Events.Count == 0)
            {
                throw new MultiplayerProtocolException(
                    $"the event history ended at sequence {after}, before sequence {throughSeq}");
            }
            // The page names every turn this replay is about to apply, so the fetches for the first
            // few can start now rather than one at a time as each turn comes round.
            PrefetchSealedSets(page, throughSeq, cancellationToken);

            foreach (var @event in page.Events)
            {
                var expected = after + 1;
                if (@event.Seq != expected)
                {
                    throw new MultiplayerProtocolException(
                        $"the event history jumped from sequence {after} to {@event.Seq}");
                }
                if (!string.Equals(@event.MatchId, _match.MatchId, StringComparison.Ordinal))
                {
                    throw new MultiplayerProtocolException(
                        $"event sequence {@event.Seq} belongs to another match");
                }
                if (@event.Seq > throughSeq) return;

                await ApplyHistoricalEventAsync(@event, cancellationToken).ConfigureAwait(false);
                after = @event.Seq;
                if (after == throughSeq) return;
            }
        }
    }

    /// <summary>
    /// Starts the fetches for the next few sealed sets this page will need.
    /// </summary>
    /// <remarks>
    /// Bounded by <see cref="SealedSetPrefetchDepth"/>, because a match of hundreds of turns would
    /// otherwise open hundreds of requests at once and hold every one of their responses in memory
    /// — which is the problem this is meant to avoid, not a bigger version of it.
    /// </remarks>
    private void PrefetchSealedSets(EventPage page, int throughSeq, CancellationToken cancellationToken)
    {
        var started = 0;
        foreach (var @event in page.Events)
        {
            if (@event.Seq > throughSeq || started >= SealedSetPrefetchDepth) return;
            if (@event is not TurnSealedEvent sealedTurn) continue;
            var turn = sealedTurn.Payload.Turn;
            if (turn < _replay.State.Coordinator.Turn || _prefetchedSealedSets.ContainsKey(turn))
                continue;
            _prefetchedSealedSets[turn] = FetchSealedSetAsync(turn, cancellationToken);
            started++;
        }
    }

    /// <summary>
    /// The sealed set for a turn, from the prefetch when it was started and from the server
    /// otherwise.
    /// </summary>
    private Task<SealedOrdersView> SealedSetAsync(int turn, CancellationToken cancellationToken)
    {
        if (_prefetchedSealedSets.Remove(turn, out var prefetched)) return prefetched;
        return FetchSealedSetAsync(turn, cancellationToken);
    }

    /// <summary>One turn's sealed set from the server, unverified.</summary>
    private Task<SealedOrdersView> FetchSealedSetAsync(int turn, CancellationToken cancellationToken) =>
        CallAsync(token => _match.SealedOrdersAsync(turn, token), _pumpLane, cancellationToken);

    private async Task ApplyHistoricalEventAsync(
        MatchEvent @event,
        CancellationToken cancellationToken)
    {
        switch (@event)
        {
            case MatchTakeoverVoteRequestedEvent requested:
                BeginTakeoverVote(requested.Payload.PlayerId, requested.Payload.Turn);
                return;
            case MatchTakeoverVoteCastEvent cast:
                RecordTakeoverVote(cast);
                return;
            case MatchTakeoverVoteCancelledEvent cancelled:
                _takeoverVotes.Remove(cancelled.Payload.PlayerId);
                return;
            case MatchPlayerTakenOverEvent takenOver:
                _takeoverVotes.Remove(takenOver.Payload.PlayerId);
                TransferPlayerToComputer(takenOver.Payload.PlayerId);
                return;
            case MatchPlayerReturnedEvent returned:
                _takeoverVotes.Remove(returned.Payload.PlayerId);
                if (returned.Payload.ReplacedComputer)
                    TransferPlayerToHuman(returned.Payload.PlayerId);
                return;
            case MatchLatePlayerJoinedEvent joined:
                AddLatePlayer(joined.Payload.PlayerId, joined.Payload.Slot);
                return;
            case TurnSealedEvent sealedTurn:
                if (sealedTurn.Payload.Turn < _replay.State.Coordinator.Turn) return;
                if (sealedTurn.Payload.Turn > _replay.State.Coordinator.Turn)
                {
                    throw new MultiplayerProtocolException(
                        $"the event history sealed turn {sealedTurn.Payload.Turn} while the "
                        + $"reconstructed match was still on turn {_replay.State.Coordinator.Turn}");
                }
                var (stateHash, _) = await FetchAndApplySealedTurnAsync(
                    sealedTurn.Payload.Turn,
                    sealedTurn.Payload.OrderSetHash,
                    cancellationToken).ConfigureAwait(false);
                // Whether this seat's own orders were in that set is not said on this path: these
                // turns are history being caught up on, and the resumed state is announced by
                // `Resumed`, which carries the submission the server holds for the open turn.
                _unreportedSeals.Add(CaptureReport(sealedTurn.Payload.Turn, stateHash, _replay.State));
                return;
            case TurnConfirmedEvent confirmed:
                VerifyHistoricalConfirmation(confirmed);
                _unreportedSeals.RemoveAll(seal => seal.Turn == confirmed.Payload.Turn);
                if (_pendingDesync is { } pending && pending.Turn == confirmed.Payload.Turn)
                    _pendingDesync = SettlePendingDesync(pending, confirmed.Payload.StateHash);
                return;
            // A divergence and its repair are FACTS about the match, not merely live notifications,
            // and the history is the only place a client that was not connected can learn them. A
            // restart during a pause replayed straight past both: nothing re-delivered the
            // `turn.desynced` behind the view's sequence, so the client re-reported the same hash
            // and waited for a repair that nobody was going to post. It is carried out of the
            // replay here and acted on once the state it is about has been rebuilt.
            case TurnDesyncedEvent desynced:
                _pendingDesync = PendingDesync.From(desynced);
                return;
        }
    }

    /// <summary>
    /// What a confirmation of the disputed turn, met in history, leaves of the pause.
    /// </summary>
    /// <remarks>
    /// The pause is over for the match, but not necessarily for this client. A client standing on
    /// the disputed turn has just had the verdict checked against its state; one rebuilt from a
    /// snapshot at or after that turn holds the repair by construction. Either way nothing is left
    /// to do. A client that was already past the turn — the live state a resync or a sequence gap
    /// carries into the replay — is still on whatever it computed, and clearing the pause here used
    /// to leave it there: the <c>snapshot.available</c> that would have repaired it is also behind
    /// the cursor now, and the next report desynced the match all over again. That client keeps the
    /// pause, marked with the verdict, and settles it once the replay is over; see
    /// <see cref="ResolvePendingDesyncAsync"/>.
    /// </remarks>
    private PendingDesync? SettlePendingDesync(PendingDesync pending, string confirmedStateHash) =>
        pending.Turn <= _canonicalThroughTurn
            ? null
            : pending with { SettledStateHash = confirmedStateHash };

    /// <summary>
    /// Checks a historical confirmation against the local state when that state is the one it is
    /// about.
    /// </summary>
    private void VerifyHistoricalConfirmation(TurnConfirmedEvent confirmed)
    {
        var resolvedTurn = _replay.State.Coordinator.Turn - 1;
        if (confirmed.Payload.Turn < resolvedTurn) return;
        if (confirmed.Payload.Turn > resolvedTurn)
        {
            throw new MultiplayerProtocolException(
                $"the event history confirmed turn {confirmed.Payload.Turn} while the "
                + $"reconstructed match had resolved only turn {resolvedTurn}");
        }
        var actual = MatchStateHasher.ComputeFingerprint(_replay.State);
        if (!string.Equals(actual, confirmed.Payload.StateHash, StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                $"the reconstructed state for confirmed turn {confirmed.Payload.Turn} does not "
                + "match the server hash; the match was produced by incompatible game rules");
        }
        _canonicalThroughTurn = confirmed.Payload.Turn;
    }

    /// <summary>
    /// The saved draft's own consistency; whether it still applies is proven by building the turn.
    /// </summary>
    private static void ValidateResumeSubmission(OwnSubmissionView submission, int currentTurn)
    {
        if (submission.Turn != currentTurn)
        {
            throw new MultiplayerProtocolException(
                $"the saved draft is for turn {submission.Turn}, but the server is on turn "
                + currentTurn);
        }
        if (submission.Orders is null)
        {
            if (submission.Ready || submission.OrdersHash is not null)
            {
                throw new MultiplayerProtocolException(
                    "the saved draft has readiness or a digest but no order document");
            }
            return;
        }
        var digest = OrderDigest.OfDocument(submission.Orders);
        if (!string.Equals(digest, submission.OrdersHash, StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                "the saved draft does not match the digest the server returned");
        }
    }
}
