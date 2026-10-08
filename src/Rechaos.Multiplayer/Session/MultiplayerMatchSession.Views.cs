using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Resolution;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// A match played from views (docs/MULTIPLAYER.md, "Planning on a view"): the server resolves
/// every turn and serves this seat its own view of each, which the interface plans on.
/// </summary>
/// <remarks>
/// <para>
/// This client holds no match of its own and resolves nothing. It does not replay sealed sets,
/// report hashes, upload snapshots or take part in a desync repair; the order document it sends
/// is the same as in any match. <c>turn.confirmed</c> of turn n is the signal that the view of
/// turn n+1 can be read, and the view arrives through the same notice a resolved turn does, so the
/// interface plays both kinds of match the same way.
/// </para>
/// <para>
/// The view is a <see cref="MatchState"/> with <see cref="MatchState.ViewedBy"/> set, which throws
/// on any call that draws or resolves. Nothing here makes one; the planning copy built over it is
/// already at this seat's planning entry.
/// </para>
/// </remarks>
public sealed partial class MultiplayerMatchSession
{
    /// <summary>Attempts at a view the server has not prepared before the player is told.</summary>
    private const int QuietViewAttempts = 4;

    private static readonly TimeSpan LongestViewWait = TimeSpan.FromSeconds(5);

    /// <summary>The roster's seats in a match played from views, which keeps no history.</summary>
    private readonly Dictionary<string, int> _viewSeats;

    /// <summary>
    /// The turn the match was on when this session started, and from then on the turn of the last
    /// view handed to the interface: the turn being planned.
    /// </summary>
    private int _viewTurn;

    /// <summary>Set once the server says this seat has been eliminated; see <see cref="EnterSeatOut"/>.</summary>
    private bool _seatOut;

    /// <summary>Set once the final state has gone to the interface, which needs it once.</summary>
    private bool _finalStateDelivered;

    private MatchReplayRecorder? _releasedJournal;

    /// <summary>
    /// The journal of the whole match, rebuilt from what the server releases once a match played
    /// from views ends, or null before then, in a lockstep match, or when the rebuild failed.
    /// </summary>
    /// <remarks>
    /// While a match played from views runs, the client holds its seat's view and nothing more, so
    /// a bug report can carry only that and the turn being planned, and such a journal does not
    /// replay as a match (<see cref="SeatViewJournal"/>). Once the match has ended the
    /// server releases the seed and every sealed set, and the session folds them into this journal
    /// after it has handed the game the final state. It is set once and never changes after, so the
    /// game may read it from its own thread.
    /// </remarks>
    public MatchReplayRecorder? ReleasedJournal => Volatile.Read(ref _releasedJournal);

    /// <summary>
    /// Set once this session has handed the interface a view, the final state or the seat's exit,
    /// so a later restore is a reconnect of a match the player is already looking at.
    /// </summary>
    private bool _interfaceHoldsTheMatch;

    /// <summary>
    /// Whether this match is played from per-seat views, as the server stamped it at creation.
    /// </summary>
    public bool PlaysFromViews => _history is null;

    /// <summary>
    /// Starts a session on a match played from views. It always starts by restoring: the first
    /// thing it does is read this seat's view of the open turn, which arrives as
    /// <see cref="MultiplayerNotice.Resumed"/>.
    /// </summary>
    private static MultiplayerMatchSession StartFromViews(
        MultiplayerSessionOptions options,
        PlayerView self)
    {
        var session = new MultiplayerMatchSession(
            options, replay: null, self, MatchHistory.SeatsOf(options.View.Players), isRestoring: true);
        // No reporter: nobody reports a hash in a match played from views.
        session._pump = Task.Run(() => session.RunPumpAsync(session._stoppingToken));
        session._outbox = Task.Run(() => session.RunOutboxAsync(session._stoppingToken));
        return session;
    }

    /// <summary>
    /// The events a match played from views treats differently.
    /// </summary>
    /// <returns>
    /// True when the event was handled here, false when the shared handling still applies.
    /// </returns>
    private async Task<bool> HandleInViewMatchAsync(MatchEvent @event, CancellationToken cancellationToken)
    {
        switch (@event)
        {
            case TurnSealedEvent sealedTurn:
                // Nothing to resolve: the server does that, and confirms the turn when it has.
                lock (_outboxGate) _locallyReadyTurns.Remove(sealedTurn.Payload.Turn);
                RetireDraftsThrough(sealedTurn.Payload.Turn);
                return true;
            case TurnConfirmedEvent confirmed:
                await AdvanceToNextViewAsync(
                        confirmed.Payload.Turn, confirmed.Payload.StateHash, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case TurnOpenedEvent opened when _seatOut:
                _viewTurn = Math.Max(_viewTurn, opened.Payload.Turn);
                PassTurn(opened.Payload.Turn);
                // No deadline to show: the game cleared it when the seat went out.
                return true;
            case TurnDeadlineExtendedEvent when _seatOut:
                return true;
            case MatchStatusChangedEvent { Payload.Status: MatchStatus.Finished }:
                // The final state goes before the finish, which the interface reads it beside.
                await DeliverFinalStateAsync(includedOwnOrders: false, cancellationToken)
                    .ConfigureAwait(false);
                return false;
            case TurnDesyncedEvent or SnapshotAvailableEvent or TurnDivergedEvent:
                // There is no client state to diverge or to repair. A snapshot the server writes is
                // its own checkpoint, released to members only once the match has ended.
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Reads the view of the turn after <paramref name="confirmedTurn"/> and hands it over, or the
    /// final state when that turn ended the match.
    /// </summary>
    private async Task AdvanceToNextViewAsync(
        int confirmedTurn,
        string stateHash,
        CancellationToken cancellationToken)
    {
        if (_finalStateDelivered || _seatOut || _ownSeatIsComputerControlled) return;
        // A repeat, or a confirmation the restore already acted on: its successor's view is in hand.
        if (confirmedTurn < _viewTurn) return;
        var includedOwnOrders = await HeldOwnOrdersAsync(confirmedTurn, cancellationToken)
            .ConfigureAwait(false);
        switch (await FetchViewAsync(cancellationToken).ConfigureAwait(false))
        {
            case ViewFetch.Served served when served.View.Coordinator.Turn != confirmedTurn + 1:
                // The pump is behind: the server has confirmed later turns since this one. Their
                // own confirmations follow on the stream, and the last of them hands this view over
                // under the turn, hash and own-orders answer it belongs to.
                return;
            case ViewFetch.Served served:
                HandOverView(served.View, stateHash, includedOwnOrders);
                return;
            case ViewFetch.Ended:
                await DeliverFinalStateAsync(includedOwnOrders, cancellationToken).ConfigureAwait(false);
                return;
            case ViewFetch.Out:
                EnterSeatOut(confirmedTurn + 1);
                return;
            case ViewFetch.NotHuman:
                // The other players gave the seat to the computer. The takeover event says so to
                // the interface; the server serves a computer seat no view.
                _ownSeatIsComputerControlled = true;
                return;
            case ViewFetch.Abandoned:
                // Announced by the status change that follows on the stream.
                return;
        }
    }

    /// <summary>
    /// Whether the server holds an order document from this seat for a turn that has sealed.
    /// </summary>
    /// <remarks>
    /// The sealed set is withheld until the match ends, so this is the nearest a seat can come to
    /// asking whether its orders made the turn: the seal froze the document the server held, and
    /// this reads that document back.
    /// </remarks>
    private async Task<bool> HeldOwnOrdersAsync(int turn, CancellationToken cancellationToken)
    {
        try
        {
            var submission = await CallAsync(
                token => _match.OwnSubmissionAsync(turn, token), _pumpLane, cancellationToken)
                .ConfigureAwait(false);
            return submission.Orders is not null;
        }
        catch (MultiplayerApiException)
        {
            return false;
        }
    }

    /// <summary>Hands the interface a view to plan the turn it is at the planning entry of.</summary>
    private void HandOverView(MatchState view, string stateHash, bool includedOwnOrders)
    {
        _viewTurn = view.Coordinator.Turn;
        _interfaceHoldsTheMatch = true;
        // The planning copy is a copy of the view, so the interface owns both independently.
        var planning = SpeculativeTurn.For(view, _definitions, Slot);
        _notices.Enqueue(new MultiplayerNotice.TurnResolved(
            _viewTurn - 1, view, stateHash, includedOwnOrders, planning));
    }

    /// <summary>
    /// Reconnects to a match played from views: the open turn's view and this seat's draft for it,
    /// or the final state of a match that has ended.
    /// </summary>
    /// <param name="replayFromSeq">
    /// Where the event history is read from for what lives only there: open takeover votes.
    /// </param>
    /// <returns>False when the match was abandoned and there is nothing left to pump.</returns>
    private async Task<bool> ResumeFromViewAsync(int replayFromSeq, CancellationToken cancellationToken)
    {
        while (true)
        {
            var view = await ReadMatchViewAsync(cancellationToken).ConfigureAwait(false);
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
            var fetched = view.Status == MatchStatus.Finished
                ? new ViewFetch.Ended()
                : await FetchViewAsync(cancellationToken).ConfigureAwait(false);
            // The match can move on between the two reads; the view is the authority, so it is
            // read again rather than resumed on a turn the server has left.
            if (fetched is ViewFetch.Served moved && moved.View.Coordinator.Turn != view.CurrentTurn)
                continue;
            await ReplayEventHistoryAsync(replayFromSeq, view.LastEventSeq, cancellationToken)
                .ConfigureAwait(false);
            _resumeAfterSeq = view.LastEventSeq;
            switch (fetched)
            {
                case ViewFetch.Served served:
                    await ResumePlanningAsync(view, served.View, cancellationToken).ConfigureAwait(false);
                    return true;
                case ViewFetch.Ended:
                    var (_, final, finalHash, rebuilt) =
                        await ReadFinalStateAsync(cancellationToken).ConfigureAwait(false);
                    _finalStateDelivered = true;
                    _interfaceHoldsTheMatch = true;
                    _notices.Enqueue(new MultiplayerNotice.Resumed(
                        view, final, new OwnSubmissionView(view.CurrentTurn, null, Ready: false, null), null));
                    await ReleaseJournalAsync(rebuilt, finalHash, cancellationToken).ConfigureAwait(false);
                    return true;
                case ViewFetch.Out:
                    _viewTurn = view.CurrentTurn;
                    EnterSeatOut(view.CurrentTurn);
                    return true;
                case ViewFetch.Abandoned:
                    _notices.Enqueue(new MultiplayerNotice.MatchAbandoned());
                    return false;
                case ViewFetch.NotHuman when _ownSeatIsComputerControlled && _interfaceHoldsTheMatch:
                    // A seat handed to the computer goes on watching, as in lockstep; a reconnect
                    // after the takeover must not end the session over the view it has no right to.
                    // A first start has nothing to show the player, so it still fails below.
                    return true;
                default:
                    throw new MultiplayerProtocolException(
                        "this seat is not held by a human player, so the server serves it no view");
            }
        }
    }

    /// <summary>The open turn's view, with this seat's draft for it restored on the planning copy.</summary>
    private async Task ResumePlanningAsync(
        MatchView view,
        MatchState state,
        CancellationToken cancellationToken)
    {
        var submission = await CallAsync(
            token => _match.OwnSubmissionAsync(view.CurrentTurn, token),
            _pumpLane,
            cancellationToken).ConfigureAwait(false);
        ValidateResumeSubmission(submission, view.CurrentTurn);
        var turn = submission.Orders is { } document
            ? SpeculativeTurn.Restore(state, _definitions, Slot, document)
            : SpeculativeTurn.For(state, _definitions, Slot);
        _viewTurn = state.Coordinator.Turn;
        _interfaceHoldsTheMatch = true;
        _notices.Enqueue(new MultiplayerNotice.Resumed(view, state, submission, turn));
        foreach (var vote in _takeoverVotes.Values.OrderBy(item => item.PlayerId, StringComparer.Ordinal))
            PublishTakeoverVote(vote);
        if (SeedReadiness(view) | _takeoverVotes.Keys.Any(_departedPlayerIds.Contains))
            PublishReadiness();
    }

    /// <summary>
    /// This seat's view of the open turn, asking again while the server has not prepared it.
    /// </summary>
    /// <remarks>
    /// The server answers <c>view_not_ready</c> until it has resolved the turn before, and a
    /// request is also what makes it try again after its resolver failed, so asking is the way to
    /// wait. The match view is read between attempts so that a match that ends or is abandoned
    /// meanwhile is not waited on. The pump's lane is told only after a few quiet attempts, and
    /// directly: the refusal proves the server answered, which every call would otherwise report
    /// as a recovery.
    /// </remarks>
    private async Task<ViewFetch> FetchViewAsync(CancellationToken cancellationToken)
    {
        // Every answer after the lane was told proves the server is back, whichever answer it is.
        var told = false;
        ViewFetch Answered(ViewFetch fetch)
        {
            if (told) _pumpLane.Recovered();
            return fetch;
        }
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var served = await CallAsync(
                    token => _match.SeatViewAsync(token), lane: null, cancellationToken)
                    .ConfigureAwait(false);
                return Answered(new ViewFetch.Served(ReadServedView(served)));
            }
            catch (MultiplayerApiException exception) when (exception.Reason == "seat_out")
            {
                return Answered(new ViewFetch.Out());
            }
            catch (MultiplayerApiException exception) when (exception.Reason == "match_finished")
            {
                return Answered(new ViewFetch.Ended());
            }
            catch (MultiplayerApiException exception) when (exception.Reason == "not_active")
            {
                return Answered(new ViewFetch.NotHuman());
            }
            catch (MultiplayerApiException exception)
                when (exception.Reason is "view_not_ready" or "match_not_running")
            {
                if (attempt >= QuietViewAttempts)
                {
                    _pumpLane.Failed(
                        "The server has not resolved the last turn yet. The match carries on as "
                        + "soon as it has.",
                        attempt);
                    told = true;
                }
                await Task.Delay(ViewRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
                var detail = await CallAsync(
                    token => _match.GetAsync(token), lane: null, cancellationToken).ConfigureAwait(false);
                if (detail.Match.Status == MatchStatus.Finished) return Answered(new ViewFetch.Ended());
                if (detail.Match.Status == MatchStatus.Abandoned) return Answered(new ViewFetch.Abandoned());
            }
        }
    }

    private static TimeSpan ViewRetryDelay(int attempt) => TimeSpan.FromMilliseconds(
        Math.Min(LongestViewWait.TotalMilliseconds, 250 * Math.Pow(2, Math.Min(attempt - 1, 5))));

    /// <summary>
    /// A served view, checked to be this seat's planning entry of the turn it names in a session
    /// and save format this build plays.
    /// </summary>
    private MatchState ReadServedView(ServedSeatView served)
    {
        RequireResumableSession(served.SessionVersion, "view");
        if (served.Slot != Slot)
        {
            throw new MultiplayerProtocolException(
                $"the server served slot {served.Slot}'s view to slot {Slot}");
        }
        if (served.FormatVersion > NativeSaveSerializer.CurrentFormatVersion)
        {
            throw new MultiplayerProtocolException(
                $"the view of turn {served.Turn} is save format {served.FormatVersion}, and this build "
                + $"reads up to {NativeSaveSerializer.CurrentFormatVersion}");
        }
        MatchState view;
        try
        {
            view = MatchStateClone.ViewFromBase64(served.Body, _definitions, new PlayerId(Slot));
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            throw new MultiplayerProtocolException(
                $"the view of turn {served.Turn} is not one this build can read: {exception.Message}",
                exception);
        }
        if (view.Outcome is not null
            || view.Coordinator.Phase != TurnPhase.Command
            || view.Coordinator.Turn != served.Turn
            || view.Coordinator.ActivePlayer != new PlayerId(Slot))
        {
            throw new MultiplayerProtocolException(
                $"the view served for turn {served.Turn} is not this seat's planning entry of it");
        }
        return view;
    }

    /// <summary>
    /// Says the seat is out of the match, and passes the turn for it.
    /// </summary>
    /// <remarks>
    /// The server holds the turn for every human seat on the roster, and only the resolver knows a
    /// seat has been eliminated, so a seat that stopped answering would keep the table waiting on
    /// every turn until the clock ran out, or for ever on a match without one. Passing is what an
    /// eliminated player in a lockstep match does by hand; the resolver gives an eliminated seat's
    /// document nothing to act on.
    /// </remarks>
    private void EnterSeatOut(int turn)
    {
        if (!_seatOut)
        {
            _seatOut = true;
            _interfaceHoldsTheMatch = true;
            _notices.Enqueue(new MultiplayerNotice.SeatOut(turn));
        }
        // Passed again on a restore of a seat already out: the turn.opened that would have passed
        // the open turn may be what the restore skipped over. The document replaces what is held.
        PassTurn(turn);
    }

    private void PassTurn(int turn) => QueueOrders(
        turn, new OrderDocument(OrderDocumentBuilder.OrderDocumentSchemaVersion, []), ready: true);

    /// <summary>Hands the interface the final state of a match that has just ended, once.</summary>
    private async Task DeliverFinalStateAsync(bool includedOwnOrders, CancellationToken cancellationToken)
    {
        if (_finalStateDelivered) return;
        var (turn, final, stateHash, rebuilt) =
            await ReadFinalStateAsync(cancellationToken).ConfigureAwait(false);
        _finalStateDelivered = true;
        _interfaceHoldsTheMatch = true;
        _notices.Enqueue(new MultiplayerNotice.TurnResolved(
            turn, final, stateHash, includedOwnOrders, Planning: null));
        await ReleaseJournalAsync(rebuilt, stateHash, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Keeps the whole match's journal for bug reports, rebuilding the match when the final state
    /// came from a snapshot.
    /// </summary>
    /// <remarks>
    /// The final state is on the screen by now, so a rebuild that fails costs only the journal: a
    /// report filed afterwards goes out without one, as a report from a match with nothing to
    /// attach does. Any failure short of the session stopping is swallowed here, because one that
    /// reached the pump would fail a session whose match ended well. A rebuild that stops short of
    /// the final state the game was handed, as a log that ends early does, is not kept either: it
    /// is not the whole match.
    /// </remarks>
    /// <param name="stateHash">The hash of the final state the game was handed.</param>
    private async Task ReleaseJournalAsync(
        AuthoritativeMatch? rebuilt, string stateHash, CancellationToken cancellationToken)
    {
        // A reconnect after the end resumes onto the final state again, and the journal is held.
        if (Volatile.Read(ref _releasedJournal) is not null) return;
        try
        {
            rebuilt ??= await RebuildReleasedMatchAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return;
        }
        if (!rebuilt.IsFinished || !string.Equals(rebuilt.StateHash, stateHash, StringComparison.Ordinal))
            return;
        Volatile.Write(ref _releasedJournal, rebuilt.Recorder);
    }

    /// <summary>
    /// The whole final state of a match played from views, which the server releases once it ends.
    /// </summary>
    /// <remarks>
    /// The server's checkpoint of the finishing turn when there is one, checked to hash to what it
    /// is stored under, and otherwise the match rebuilt from the seed and the sealed sets, which
    /// are released at the same moment.
    /// </remarks>
    private async Task<(int Turn, MatchState State, string StateHash, AuthoritativeMatch? Rebuilt)>
        ReadFinalStateAsync(CancellationToken cancellationToken)
    {
        if (await LatestSnapshotOrNullAsync(cancellationToken).ConfigureAwait(false) is { } snapshot)
        {
            var state = ReadVerifiedSnapshot(snapshot);
            if (state.Outcome is not null) return (snapshot.Turn, state, snapshot.StateHash, null);
        }
        var rebuilt = await RebuildReleasedMatchAsync(cancellationToken).ConfigureAwait(false);
        var final = MatchStateClone.Of(rebuilt.Recorder.State, _definitions);
        return (final.Coordinator.Turn - 1, final, rebuilt.StateHash, rebuilt);
    }

    /// <summary>
    /// The whole match, rebuilt from what the server releases once a match played from views ends:
    /// the seed, the roster <c>match.started</c> seated, and every sealed set.
    /// </summary>
    /// <remarks>
    /// The log is folded through <see cref="AuthoritativeMatch"/>, which is what the server's
    /// resolver folds it with, and every confirmation in it is held to the state reached, so a
    /// rebuild that does not reach what the server confirmed is refused rather than shown.
    /// </remarks>
    /// <exception cref="MultiplayerApiException">
    /// The match has not ended, so the server still withholds what the rebuild needs.
    /// </exception>
    private async Task<AuthoritativeMatch> RebuildReleasedMatchAsync(CancellationToken cancellationToken)
    {
        var view = await ReadMatchViewAsync(cancellationToken).ConfigureAwait(false);
        var seed = view.Seed
            ?? throw new MultiplayerProtocolException("the server has not released the match's seed");
        var settings = MultiplayerGameSettings.FromWire(view.Settings.GameSettings);
        AuthoritativeMatch? match = null;
        var after = 0;
        var reachedEnd = false;
        while (!reachedEnd && after < view.LastEventSeq)
        {
            var page = await CallAsync(
                token => _match.EventsAsync(after, EventHistoryPageSize, token),
                _pumpLane,
                cancellationToken).ConfigureAwait(false);
            // Rows the server withholds leave gaps, and a page of nothing but them is the log's end.
            if (page.Events.Count == 0) break;
            foreach (var @event in page.Events)
            {
                if (@event.Seq <= after)
                {
                    throw new MultiplayerProtocolException(
                        $"the event history went back from sequence {after} to {@event.Seq}");
                }
                if (@event.Seq > view.LastEventSeq)
                {
                    reachedEnd = true;
                    break;
                }
                after = @event.Seq;
                if (@event is MatchStartedEvent started)
                {
                    match ??= AuthoritativeMatch.Bootstrap(
                        _definitions, seed, settings, started.Payload.Players);
                    continue;
                }
                if (match is null) continue;
                switch (@event)
                {
                    case TurnSealedEvent sealedTurn
                        when !match.IsFinished && sealedTurn.Payload.Turn >= match.Turn:
                        var sealedOrders = await FetchSealedSetAsync(sealedTurn.Payload.Turn, cancellationToken)
                            .ConfigureAwait(false);
                        match.Apply(sealedTurn, sealedOrders);
                        break;
                    case TurnConfirmedEvent confirmed when confirmed.Payload.Turn == match.Turn - 1:
                        if (!string.Equals(match.StateHash, confirmed.Payload.StateHash, StringComparison.Ordinal))
                        {
                            throw new MultiplayerProtocolException(
                                $"the released history does not reach the state the server confirmed for "
                                + $"turn {confirmed.Payload.Turn}");
                        }
                        break;
                    default:
                        match.Apply(@event);
                        break;
                }
            }
        }
        return match ?? throw new MultiplayerProtocolException(
            "the released history has no match.started to rebuild the match from");
    }

    /// <summary>
    /// A history event of a match played from views: only what lives in the log and nowhere else
    /// matters, since the state comes from the view.
    /// </summary>
    private void ApplyHistoricalViewEvent(MatchEvent @event)
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
                if (string.Equals(takenOver.Payload.PlayerId, PlayerId, StringComparison.Ordinal))
                    _ownSeatIsComputerControlled = true;
                return;
            case MatchPlayerReturnedEvent returned:
                _takeoverVotes.Remove(returned.Payload.PlayerId);
                return;
            case MatchLatePlayerJoinedEvent joined:
                AddLatePlayer(joined.Payload.PlayerId, joined.Payload.Slot);
                return;
            case TurnReadinessEvent readiness:
                RecordReadiness(readiness.Payload.Turn, readiness.Payload.PlayerId, readiness.Payload.Ready);
                return;
        }
    }

    /// <summary>What asking for this seat's view came to.</summary>
    private abstract record ViewFetch
    {
        /// <summary>The view of the open turn.</summary>
        internal sealed record Served(MatchState View) : ViewFetch;

        /// <summary>The seat has been eliminated and has no view.</summary>
        internal sealed record Out : ViewFetch;

        /// <summary>The match has ended; its whole state is released.</summary>
        internal sealed record Ended : ViewFetch;

        /// <summary>The seat is held by the computer, which plans it on the server.</summary>
        internal sealed record NotHuman : ViewFetch;

        /// <summary>The server gave up on the match.</summary>
        internal sealed record Abandoned : ViewFetch;
    }
}
