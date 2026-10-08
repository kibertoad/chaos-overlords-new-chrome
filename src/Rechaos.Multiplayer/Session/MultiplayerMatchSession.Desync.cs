using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Turning a detected divergence back into a playable match.
/// </summary>
/// <remarks>
/// <para>
/// A desync pauses the match until somebody posts the state every client then converges on. The
/// server holds whoever posts it to a hash the players themselves reported in the greatest number,
/// so a repair cannot be minted; what this file decides is whether THIS client is holding such a
/// state, how to get back to it when the match has moved on, and how to adopt somebody else's.
/// </para>
/// <para>
/// All of it used to hang off the live <c>turn.desynced</c> event reaching a host that was still
/// standing on the disputed turn, which is a narrow window on a path that only opens when something
/// has already gone wrong. A desync noticed after the next turn sealed, a restart during the pause,
/// a peer that came back after the repair was posted, and a host that is itself the outlier each
/// left the match paused until retention collected it. Every one of them is answered here.
/// </para>
/// </remarks>
public sealed partial class MultiplayerMatchSession
{
    /// <summary>A divergence the log has announced and no verdict has settled since.</summary>
    /// <param name="Turn">The disputed turn.</param>
    /// <param name="Candidates">The hashes a repair may claim, as the announcement named them.</param>
    /// <param name="Details">Short hashes per player, for the interface and the diagnostics log.</param>
    /// <param name="Reports">The hash each human seat reported, by player id.</param>
    /// <param name="TieBreakerPlayerId">
    /// Who may break a tie between the candidates, as the announcement named them. Null when there
    /// is one candidate, and also on an announcement stored before the field existed, whose ties
    /// only the host could break.
    /// </param>
    /// <param name="SettledStateHash">
    /// The hash the log went on to confirm for the turn, when it did so while this client was
    /// already past it; see <see cref="SettlePendingDesync"/>. Null while the verdict is still open.
    /// </param>
    private sealed record PendingDesync(
        int Turn,
        IReadOnlyList<string> Candidates,
        string Details,
        IReadOnlyDictionary<string, string> Reports,
        string? TieBreakerPlayerId,
        string? SettledStateHash = null)
    {
        public static PendingDesync From(TurnDesyncedEvent desynced) => new(
            desynced.Payload.Turn,
            desynced.Payload.CandidateStateHashes,
            string.Join(", ", desynced.Payload.Reports
                .OrderBy(report => report.PlayerId, StringComparer.Ordinal)
                .Select(report => $"{report.PlayerId}:{ShortHash(report.StateHash)}")),
            desynced.Payload.Reports
                .GroupBy(report => report.PlayerId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().StateHash,
                    StringComparer.Ordinal),
            desynced.Payload.TieBreakerPlayerId);
    }

    /// <summary>
    /// The reports this client has finished checking against a rebuild, by disputed turn.
    /// </summary>
    /// <remarks>
    /// A desync is announced again whenever its verdict changes, and a rebuild already compared
    /// with a report would reach the same answer again: nothing it is built from has changed. Keyed
    /// on the report as well as the turn, so a report this client changed since is checked afresh,
    /// and an announcement that still carries the report a correction replaced is not corrected a
    /// second time. A report is marked only once its check is over, so a check that a failed fetch
    /// cut short is tried again rather than taken for one that agreed.
    /// </remarks>
    private readonly HashSet<(int Turn, string ReportedHash)> _selfChecked = [];

    /// <summary>
    /// The divergence this client is waiting on, or null.
    /// </summary>
    /// <remarks>
    /// Written by the pump on the live path and by the history replay on the restore path, which is
    /// what lets a restart pick a pause back up: the <c>turn.desynced</c> event that announced it
    /// sits behind the view's sequence and is never delivered again.
    /// </remarks>
    private PendingDesync? _pendingDesync;

    /// <summary>The latest turn the local state is known to agree with the server on.</summary>
    /// <remarks>
    /// Set by a snapshot the state was rebuilt from, a repair it adopted, or a historical
    /// confirmation checked against it. No divergence at or before this turn can survive in the
    /// local state, which is what lets a confirmation of an older disputed turn close the pause
    /// without a rebuild. Zero to begin with: every client generates the bootstrap identically.
    /// </remarks>
    private int _canonicalThroughTurn;

    /// <summary>
    /// Whether the interface was last told the match is paused, so it is standing on the Desynced
    /// stage with planning closed.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="ShowPause"/>, and cleared by every notice that reopens planning:
    /// <see cref="MultiplayerNotice.Resumed"/>, <see cref="MultiplayerNotice.TurnResolved"/>,
    /// <see cref="MultiplayerNotice.Resynced"/> and <see cref="MultiplayerNotice.PauseLifted"/>.
    /// The server lifts the pause once no turn of the match is unsettled and says so with
    /// <c>match.statusChanged</c>, which <see cref="LiftPauseAsync"/> answers. A pause that ends
    /// with this client adopting a repair it did not hold reopens planning through
    /// <see cref="MultiplayerNotice.Resynced"/>; every other ending used to leave the interface on
    /// the Desynced stage for good.
    /// </remarks>
    private bool _pauseShown;

    /// <summary>Tells the interface the match is paused.</summary>
    private void ShowPause(MultiplayerNotice.Desynced notice)
    {
        _pauseShown = true;
        _notices.Enqueue(notice);
    }

    /// <summary>Hands the interface a state with planning reopened on it.</summary>
    private void ReopenPlanning(MultiplayerNotice notice)
    {
        _pauseShown = false;
        _notices.Enqueue(notice);
    }

    /// <summary>
    /// The server says the match is running again. Reopen planning when the interface is still
    /// showing the pause.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lift is logged after everything that settled the pause: a posted repair's
    /// <c>snapshot.available</c> and every <c>turn.confirmed</c> come first, and the pump handles
    /// events in log order. So by the time this runs, a client that needed the repair has adopted
    /// it and said so with <see cref="MultiplayerNotice.Resynced"/>, and the clients still showing
    /// the pause are the ones whose state was already the one the match settled on.
    /// </para>
    /// <para>
    /// Orders were refused for the whole pause, but a draft saved before it is still the server's
    /// for the open turn, so the planning copy is built from it the way a restore builds one. A
    /// draft that no longer applies is one this client saved on a state it has since corrected; it
    /// is left out of the planning copy, and the next edit replaces it on the server. A finished
    /// document that no longer applies cannot be replaced, so the seat stays finished.
    /// </para>
    /// </remarks>
    private async Task LiftPauseAsync(CancellationToken cancellationToken)
    {
        if (!_pauseShown) return;
        var state = MatchStateClone.Of(_replay.State, _definitions);
        var turn = state.Coordinator.Turn;
        if (!IsPlannable(state))
        {
            ReopenPlanning(new MultiplayerNotice.PauseLifted(turn, state, null, null));
            return;
        }
        var submission = await CallAsync(
                token => _match.OwnSubmissionAsync(turn, token), _pumpLane, cancellationToken)
            .ConfigureAwait(false);
        ValidateResumeSubmission(submission, turn);
        SpeculativeTurn? planning = null;
        if (submission.Orders is { } document)
        {
            // Another schema or another seat's ops fail the session here as they do on a restore;
            // only a document written on a state this client has since corrected is let go below.
            SpeculativeTurn.EnsureReadable(document, Slot);
            try
            {
                planning = SpeculativeTurn.Restore(state, _definitions, Slot, document);
            }
            catch (MultiplayerProtocolException)
            {
                // A finished document stays finished: the server answers a later draft with the
                // document that stands. So a ready seat keeps its readiness and digest and waits for
                // the seal; only a plain draft is dropped for the next edit to replace.
                submission = new OwnSubmissionView(
                    turn,
                    null,
                    submission.Ready,
                    submission.Ready ? submission.OrdersHash : null);
            }
        }
        planning ??= SpeculativeTurn.For(state, _definitions, Slot);
        // A finished turn queued before the pause was refused with `match_desynced`, which leaves
        // its readiness in the outbox. Carried forward, the first draft after the lift would end
        // the turn for a player who is being shown it open. A ready document still queued for the
        // turn is newer than the server's answer and keeps its readiness.
        if (!submission.Ready) WithdrawReadiness(turn);
        ReopenPlanning(new MultiplayerNotice.PauseLifted(turn, state, submission, planning));
    }

    /// <summary>The match paused because clients disagreed about a turn.</summary>
    private Task HandleDesyncAsync(TurnDesyncedEvent desynced, CancellationToken cancellationToken)
    {
        _pendingDesync = PendingDesync.From(desynced);
        // The live path does not go looking for a repair: `snapshot.available` follows this event
        // in the log and will arrive on its own. Only a client picking a pause up out of history
        // has to ask, because that announcement is behind its cursor and will not come again.
        return ResolvePendingDesyncAsync(lookForAPostedRepair: false, cancellationToken);
    }

    /// <summary>
    /// Do whatever this client can about the divergence it is waiting on.
    /// </summary>
    /// <remarks>
    /// Three outcomes, in the order they are worth trying. A repair somebody already posted is
    /// adopted, because the <c>snapshot.available</c> that announced it may be behind this client's
    /// cursor and will never arrive again. Failing that, a state this client is allowed to post is
    /// posted. Failing both, it says what it is waiting for and keeps the session alive — the one
    /// thing it must not do is end, because the repair may still be coming from somebody else.
    /// </remarks>
    private async Task ResolvePendingDesyncAsync(
        bool lookForAPostedRepair,
        CancellationToken cancellationToken)
    {
        if (_pendingDesync is not { } pending) return;
        // The match settled the turn while this client was past it. Whether this client reached
        // the verdict is the only question left, and a client that did has nothing to repair.
        if (pending.SettledStateHash is { } settled
            && await HoldsStateAfterTurnAsync(pending.Turn, settled, cancellationToken)
                .ConfigureAwait(false))
        {
            _pendingDesync = null;
            return;
        }
        if (lookForAPostedRepair)
        {
            var posted = await SnapshotForTurnOrNullAsync(pending.Turn, cancellationToken)
                .ConfigureAwait(false);
            if (posted is not null)
            {
                await AdoptRepairAsync(posted, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
        // A settled turn takes no more repairs, so there is nothing left to post or to wait for.
        if (pending.SettledStateHash is not null)
        {
            _pendingDesync = null;
            return;
        }
        // Before posting this client's state or waiting for somebody else's, make sure the report
        // this client made is the one its rules reach from the server's own facts. A client whose
        // live state went wrong outside the sealed sets corrects itself here and reports again, and
        // when that makes the reports unanimous the turn settles with no snapshot at all.
        if (await CorrectOwnReportAsync(pending, cancellationToken).ConfigureAwait(false)) return;
        // The state as it stood after the disputed turn, which is not necessarily the state this
        // client is on: reports for turn N can arrive after N+1 has sealed, and in a timed match
        // one slow seat is enough to make that the ordinary case rather than a corner.
        var ours = await StateAfterTurnAsync(pending.Turn, cancellationToken).ConfigureAwait(false);
        var hash = ours is null ? null : MatchStateHasher.ComputeFingerprint(ours);
        var mayRepair = hash is not null
            && pending.Candidates.Contains(hash, StringComparer.Ordinal)
            // The server takes a repair from whoever holds the SOLE most-reported hash, and a tie
            // from the one player the announcement designated. Asking for one it would refuse only
            // costs a round trip, but saying "automatic repair in progress" and then not repairing
            // costs the player the truth.
            && (pending.Candidates.Count == 1 || IsTieBreaker(pending));
        ShowPause(new MultiplayerNotice.Desynced(
            pending.Turn,
            IsHost,
            mayRepair,
            $"LOCAL {ShortHash(hash ?? "unknown")}  REPORTS {pending.Details}"));
        if (!mayRepair) return;
        try
        {
            await UploadRepairAsync(pending.Turn, hash!, ours!, cancellationToken).ConfigureAwait(false);
        }
        catch (MultiplayerApiException exception) when (IsRepairAlreadyPosted(exception))
        {
            // Another client's repair landed first. Every client holding the sole most-reported
            // hash is entitled to post it, so on a table of three or more two of them routinely
            // race, and the loser's upload reaches a turn the winner's already settled: a peer is
            // refused `403 host_only` by the checkpoint path a confirmed turn now takes, and the
            // host `409 turn_not_desynced` once the turn has moved on. Neither says anything is
            // wrong. The pause stays, and the winner's `snapshot.available` and `turn.confirmed`,
            // which are behind this point in the log, settle it as they would for any client that
            // never tried.
        }
    }

    /// <summary>Whether this client is the one designated to break the pending tie.</summary>
    /// <remarks>
    /// An announcement stored before the designation existed names nobody, and its tie was the
    /// host's to break, which is also what a server of that age still enforces.
    /// </remarks>
    private bool IsTieBreaker(PendingDesync pending) => pending.TieBreakerPlayerId is { } designee
        ? string.Equals(designee, PlayerId, StringComparison.Ordinal)
        : IsHost;

    /// <summary>
    /// Whether a refused repair upload was refused because the turn no longer needs one, or no
    /// longer needs it from this client.
    /// </summary>
    /// <remarks>
    /// <c>not_tie_breaker</c> means the roster moved after the announcement this client acted on.
    /// The server then announces the desync again with the new designee, and that announcement
    /// brings this client back here.
    /// </remarks>
    private static bool IsRepairAlreadyPosted(MultiplayerApiException exception) =>
        exception.Reason is "host_only" or "turn_not_desynced" or "not_tie_breaker";

    /// <summary>
    /// Rebuild the disputed turn from the server's facts and, when the result is not what this
    /// client reported, adopt it and report again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rebuild starts from the newest snapshot below the turn and applies the sealed order
    /// sets, the same facts every peer's state came from. What it cannot reproduce is whatever
    /// made this client's live state differ from them, so a rebuild that disagrees with the report
    /// is the better of the two: it is adopted the way a posted repair is, caught up to the turn
    /// the live state is on, and every turn on the way is reported again. The server judges the
    /// new reports like any other, and a match whose reports now agree confirms the turn and lifts
    /// the pause with nobody uploading anything.
    /// </para>
    /// <para>
    /// Lifting the pause is the server's call. The corrected report is one more for it to judge:
    /// when that makes the reports agree, the server confirms the turn and lifts the pause, and
    /// <see cref="LiftPauseAsync"/> reopens planning then; when other seats still disagree, the
    /// match stays paused and orders are still refused. So the interface gets the corrected state
    /// with the pause still showing, and no planning copy the server would refuse.
    /// </para>
    /// <para>
    /// A rebuild that agrees with the report shows the divergence is in the rules every client
    /// runs rather than in this client's history, and only a snapshot can settle that. Nothing is
    /// changed, and the caller goes on to post or wait as before.
    /// </para>
    /// </remarks>
    /// <returns>Whether this client adopted the rebuild and reported again.</returns>
    private async Task<bool> CorrectOwnReportAsync(
        PendingDesync pending,
        CancellationToken cancellationToken)
    {
        if (!pending.Reports.TryGetValue(PlayerId, out var reported)) return false;
        if (_selfChecked.Contains((pending.Turn, reported))) return false;
        var liveTurn = _replay.State.Coordinator.Turn;
        if (liveTurn <= pending.Turn) return false;
        var baseline = await SnapshotBelowAsync(pending.Turn, cancellationToken).ConfigureAwait(false);
        if (baseline is null) return false;
        // One rebuild serves the check and the catch-up. The report it captures for the disputed
        // turn is the hash taken right after that turn resolved, before any later turn or handover
        // was applied, which is what this client reported for it.
        var rebuilt = await RebuildAsync(
                baseline,
                throughTurn: liveTurn - 1,
                handoversThroughTurn: liveTurn,
                captureReports: true,
                cancellationToken)
            .ConfigureAwait(false);
        var rebuiltHash = rebuilt.Reports
            .Where(report => report.Turn == pending.Turn)
            .Select(report => report.Request.StateHash)
            .FirstOrDefault();
        _selfChecked.Add((pending.Turn, reported));
        // No hash for the turn means the rebuilt match finished before reaching it, and a rebuild
        // that cannot speak for the turn has nothing to correct the report with.
        if (rebuiltHash is null || string.Equals(rebuiltHash, reported, StringComparison.Ordinal))
            return false;
        _replay = rebuilt.Recorder;
        _unreportedSeals.Clear();
        _selfChecked.Add((pending.Turn, rebuiltHash));
        var reports = Task.WhenAll(rebuilt.Reports.Select(QueueReportAsync).ToArray());
        await AwaitRepairReportsAsync(reports, cancellationToken).ConfigureAwait(false);
        ShowPause(new MultiplayerNotice.Desynced(
            pending.Turn,
            IsHost,
            IsRepairing: false,
            $"LOCAL {ShortHash(rebuiltHash)}  REPORTS {pending.Details}",
            MatchStateClone.Of(_replay.State, _definitions)));
        return true;
    }

    private Task UploadRepairAsync(
        int turn,
        string hash,
        MatchState ours,
        CancellationToken cancellationToken) =>
        CallAsync(
            token => _match.UploadSnapshotAsync(
                new UploadSnapshotRequest(
                    turn,
                    // The body is a native save, so the version that describes it is the native
                    // save format's — not the replay format's, which says nothing about these bytes.
                    NativeSaveSerializer.CurrentFormatVersion,
                    MultiplayerProtocolVersion.Current,
                    MultiplayerSessionVersion.Current,
                    hash,
                    MatchStateClone.ToBase64(ours),
                    SummarizeSeats(ours)),
                token),
            _pumpLane,
            cancellationToken);

    /// <summary>
    /// A repair was posted for a turn; adopt it unless this client is already on that state.
    /// </summary>
    private async Task AdoptSnapshotAsync(
        SnapshotAvailableEventPayload announced,
        CancellationToken cancellationToken)
    {
        // Only a divergence still open is worth a repair. Delivery is at least once and the log is
        // replayed on every reconnect, so the announcement of a repair long settled arrives here
        // routinely, and going back to the server to compare states for it would be work done to
        // reach the same conclusion. `turn.desynced` always precedes `snapshot.available` in the
        // log, so a repair that matters always finds its pause here.
        if (_pendingDesync?.Turn != announced.Turn) return;
        // A repair for a turn this client has not resolved yet is not something it can be behind
        // on; the seal for that turn is still ahead of it in the log and will bring it here.
        if (announced.Turn >= _replay.State.Coordinator.Turn) return;
        // Compared at the DISPUTED turn, not against wherever this client is now. Delivery is at
        // least once, so the repeat of a repair already adopted arrives here — and by then several
        // turns may have been applied on top of it, which is exactly when comparing current hashes
        // says "different" and re-adopting throws all of them away.
        if (await HoldsStateAfterTurnAsync(announced.Turn, announced.StateHash, cancellationToken)
            .ConfigureAwait(false))
        {
            _pendingDesync = null;
            return;
        }
        var snapshot = await CallAsync(
            token => _match.SnapshotAsync(announced.Turn, token), _pumpLane, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot.Turn != announced.Turn)
        {
            throw new MultiplayerProtocolException(
                $"the server answered turn {announced.Turn}'s repair with the snapshot for turn "
                + snapshot.Turn);
        }
        await AdoptRepairAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Replace the local state with a repair and catch back up to where the match is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The snapshot is the state after its own turn, so a match that has since sealed further turns
    /// needs those replayed on top of it before this client is back in step. That is the restore
    /// path's work, done here from the turn the repair names rather than from wherever the client
    /// happened to be: a repair for a turn older than the one this client last resolved used to be
    /// ignored outright, which is the state every peer is left in once reports for turn N arrive
    /// after N+1 has sealed.
    /// </para>
    /// <para>
    /// It catches up to the turn the live replay is on, not to the server's current turn. That is
    /// where the event cursor is, so the seals and handovers past it still arrive on the stream at
    /// the boundaries they belong to rather than being skipped as already applied.
    /// </para>
    /// <para>
    /// The repaired state is built off to the side and swapped in whole. A handler can still end
    /// early — a retry window closing on a sealed-set fetch — and the restore that follows starts
    /// from the live replay and the cursor that describes it; a half-repaired replay under that
    /// cursor was a state no history could be replayed onto.
    /// </para>
    /// <para>
    /// Every turn it passes through is re-reported, because the server waits for a report from
    /// every human seat and an earlier turn left unsettled blocks every later repair. Reports for
    /// turns already confirmed are refused with <c>turn_confirmed</c>, which
    /// <see cref="SendReportAsync"/> reads as the success it is.
    /// </para>
    /// </remarks>
    private async Task AdoptRepairAsync(SnapshotView snapshot, CancellationToken cancellationToken)
    {
        var liveTurn = _replay.State.Coordinator.Turn;
        var rebuilt = await RebuildAsync(
                snapshot,
                throughTurn: liveTurn - 1,
                handoversThroughTurn: liveTurn,
                captureReports: true,
                cancellationToken)
            .ConfigureAwait(false);
        _replay = rebuilt.Recorder;
        _canonicalThroughTurn = snapshot.Turn;
        _pendingDesync = null;
        // Seals reconstructed before this repair are superseded by the turns just replayed.
        _unreportedSeals.Clear();
        var reports = Task.WhenAll(rebuilt.Reports.Select(QueueReportAsync).ToArray());
        await AwaitRepairReportsAsync(reports, cancellationToken).ConfigureAwait(false);
        var current = MatchStateHasher.ComputeFingerprint(_replay.State);
        var (state, planning) = HandOver();
        ReopenPlanning(new MultiplayerNotice.Resynced(snapshot.Turn, state, current, planning));
    }

    /// <summary>
    /// Waits for the reports a repair queued, unless a resync is asked for first.
    /// </summary>
    /// <remarks>
    /// The server answers orders with <c>match_desynced</c> until the pause lifts, and the pause
    /// lifts on these reports, so the repaired turn is not handed to the player before they are
    /// answered. But the reporter retries through an outage with no bound, and a pump that waited
    /// that out ignored every resync the player asked for meanwhile. A resync request ends the wait
    /// and nothing else: the adoption is already committed, the reports stay queued in order, and
    /// the restore that follows starts from the repaired state. Outside a stream cycle — a repair
    /// adopted by a restore — there is no resync to wait for, and the restore waits as it always has.
    /// </remarks>
    private async Task AwaitRepairReportsAsync(Task reports, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _streamCycle) is not { } cycle)
        {
            await reports.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        using var interruptible = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, cycle.Token);
        try
        {
            await reports.WaitAsync(interruptible.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Forget(reports);
        }
    }

    /// <summary>
    /// Whether this client reached <paramref name="stateHash"/> after <paramref name="turn"/>.
    /// </summary>
    private async Task<bool> HoldsStateAfterTurnAsync(
        int turn,
        string stateHash,
        CancellationToken cancellationToken)
    {
        var ours = await StateAfterTurnAsync(turn, cancellationToken).ConfigureAwait(false);
        return ours is not null
            && string.Equals(
                stateHash,
                MatchStateHasher.ComputeFingerprint(ours),
                StringComparison.Ordinal);
    }

    /// <summary>
    /// The state as it stood after <paramref name="turn"/>, or null when it cannot be reached.
    /// </summary>
    /// <remarks>
    /// The live state when that is where the match is, which is the common case and costs nothing.
    /// Otherwise it is rebuilt the way a reconnect rebuilds: the newest snapshot below the turn,
    /// then the sealed sets on top of it. Those sets are immutable and served with
    /// <c>Cache-Control: immutable</c>, so the rebuild asks the server for facts it will never
    /// change its mind about.
    ///
    /// Null means this client cannot speak for that turn at all — it has not reached it, or there
    /// is no snapshot old enough to rebuild from — and a client that cannot speak for a turn simply
    /// does not offer to repair it.
    /// </remarks>
    private async Task<MatchState?> StateAfterTurnAsync(int turn, CancellationToken cancellationToken)
    {
        var reached = _replay.State.Coordinator.Turn;
        if (reached == turn + 1) return _replay.State;
        if (reached <= turn) return null;
        var baseline = await SnapshotBelowAsync(turn, cancellationToken).ConfigureAwait(false);
        if (baseline is null) return null;
        var rebuilt = await RebuildAsync(
                baseline,
                throughTurn: turn,
                handoversThroughTurn: turn,
                captureReports: false,
                cancellationToken)
            .ConfigureAwait(false);
        return rebuilt.Recorder.State;
    }

    /// <summary>A state rebuilt from a snapshot, and the report each turn on the way produced.</summary>
    private sealed record RebuiltState(MatchReplayRecorder Recorder, IReadOnlyList<TurnReport> Reports);

    /// <summary>
    /// Rebuilds the state after <paramref name="throughTurn"/> from a server-held snapshot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one place a state is rebuilt outside the event history, for both a repair to adopt and a
    /// turn to speak for. The sealed sets carry the orders, and <see cref="_controlHandovers"/> the
    /// seats that changed hands between them, which a rebuild from sealed sets alone played with
    /// their old controllers. Handovers up to <paramref name="handoversThroughTurn"/> are applied,
    /// so a caller that wants the state the hash for <paramref name="throughTurn"/> was taken from
    /// leaves out the ones made after that turn resolved.
    /// </para>
    /// <para>
    /// A finished match stops the rebuild: a turn sealed on its deadline after the match ended,
    /// because a seat had not reported, is not one a finished state can take. The fetches run
    /// <see cref="SealedSetPrefetchDepth"/> ahead of the turn being applied, as the restore's do.
    /// </para>
    /// </remarks>
    private async Task<RebuiltState> RebuildAsync(
        SnapshotView baseline,
        int throughTurn,
        int handoversThroughTurn,
        bool captureReports,
        CancellationToken cancellationToken)
    {
        var recorder = new MatchReplayRecorder(ReadVerifiedSnapshot(baseline));
        var reports = new List<TurnReport>();
        if (captureReports) reports.Add(CaptureReport(baseline.Turn, baseline.StateHash, recorder.State));
        var fetches = new Queue<Task<SealedOrdersView>>();
        var nextFetch = baseline.Turn + 1;
        try
        {
            for (var turn = baseline.Turn + 1; turn <= throughTurn; turn++)
            {
                if (recorder.State.Outcome is not null) break;
                while (fetches.Count < SealedSetPrefetchDepth && nextFetch <= throughTurn)
                    fetches.Enqueue(FetchSealedSetAsync(nextFetch++, cancellationToken));
                ApplyHandovers(recorder, afterTurn: turn - 1, throughTurn: turn);
                var sealedOrders = await fetches.Dequeue().ConfigureAwait(false);
                RequireSealedSet(sealedOrders, turn);
                var stateHash = SealedTurnApplier.Apply(recorder, sealedOrders);
                if (captureReports) reports.Add(CaptureReport(turn, stateHash, recorder.State));
            }
            ApplyHandovers(recorder, afterTurn: throughTurn, throughTurn: handoversThroughTurn);
        }
        finally
        {
            // Whatever is still in flight belongs to a rebuild that is over, one way or the other.
            foreach (var pending in fetches) Forget(pending);
        }
        return new RebuiltState(recorder, reports);
    }

    /// <summary>
    /// Throws unless a fetched sealed set is the one for <paramref name="turn"/>, carries the digest
    /// the event log announced when there is one, and matches its own digest.
    /// </summary>
    private static void RequireSealedSet(
        SealedOrdersView sealedOrders,
        int turn,
        string? announcedOrderSetHash = null)
    {
        if (sealedOrders.Turn != turn)
        {
            throw new MultiplayerProtocolException(
                $"the server answered turn {turn}'s sealed set with the set for turn {sealedOrders.Turn}");
        }
        if (announcedOrderSetHash is not null
            && !string.Equals(
                sealedOrders.OrderSetHash,
                announcedOrderSetHash,
                StringComparison.Ordinal))
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

    /// <summary>The newest snapshot before <paramref name="turn"/>, or null.</summary>
    /// <remarks>
    /// <para>
    /// Strictly before: the point of the rebuild is the state THIS client's rules reach by applying
    /// the turn. A snapshot for the turn itself is the repair somebody posted for it, and a rebuild
    /// that started there compared the repair with itself — which is how a client past the
    /// disputed turn concluded it already held the repair and never adopted it.
    /// </para>
    /// <para>
    /// The latest row usually qualifies: while a turn is unsettled no later turn can be repaired,
    /// so nothing newer than the disputed turn can have been written. The bootstrap row is the
    /// fallback, and it is the baseline every ordinary reconnect replays from anyway.
    /// </para>
    /// </remarks>
    private async Task<SnapshotView?> SnapshotBelowAsync(
        int turn,
        CancellationToken cancellationToken)
    {
        var latest = await LatestSnapshotOrNullAsync(cancellationToken).ConfigureAwait(false);
        if (latest is not null && latest.Turn < turn) return latest;
        return await SnapshotForTurnOrNullAsync(0, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SnapshotView?> SnapshotForTurnOrNullAsync(
        int turn,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CallAsync(
                token => _match.SnapshotAsync(turn, token), _pumpLane, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MultiplayerApiException exception)
            when (exception.Reason == "no_snapshot"
                || exception.Status == System.Net.HttpStatusCode.NotFound)
        {
            // "There is no snapshot for that turn" is the answer either way. The named reason is
            // what this server says; a bare 404 is what a deployment in front of it might.
            return null;
        }
    }

    private static string ShortHash(string hash) => hash[..Math.Min(12, hash.Length)];
}
