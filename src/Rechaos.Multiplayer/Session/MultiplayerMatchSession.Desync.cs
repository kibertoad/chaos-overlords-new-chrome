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
    private sealed record PendingDesync(int Turn, IReadOnlyList<string> Candidates, string Details);

    /// <summary>
    /// The divergence this client is waiting on, or null.
    /// </summary>
    /// <remarks>
    /// Written by the pump on the live path and by the history replay on the restore path, which is
    /// what lets a restart pick a pause back up: the <c>turn.desynced</c> event that announced it
    /// sits behind the view's sequence and is never delivered again.
    /// </remarks>
    private PendingDesync? _pendingDesync;

    /// <summary>The match paused because clients disagreed about a turn.</summary>
    private Task HandleDesyncAsync(TurnDesyncedEvent desynced, CancellationToken cancellationToken)
    {
        var details = string.Join(", ", desynced.Payload.Reports
            .OrderBy(report => report.PlayerId, StringComparer.Ordinal)
            .Select(report => $"{report.PlayerId}:{ShortHash(report.StateHash)}"));
        _pendingDesync = new PendingDesync(
            desynced.Payload.Turn, desynced.Payload.CandidateStateHashes, details);
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
        // The state as it stood after the disputed turn, which is not necessarily the state this
        // client is on: reports for turn N can arrive after N+1 has sealed, and in a timed match
        // one slow seat is enough to make that the ordinary case rather than a corner.
        var ours = await StateAfterTurnAsync(pending.Turn, cancellationToken).ConfigureAwait(false);
        var hash = ours is null ? null : MatchStateHasher.ComputeSha256(ours);
        var mayRepair = hash is not null
            && pending.Candidates.Contains(hash, StringComparer.Ordinal)
            // The server takes a repair from whoever holds the SOLE most-reported hash, and leaves
            // a tie to the host. Asking for one it would refuse only costs a round trip, but saying
            // "automatic repair in progress" and then not repairing costs the player the truth.
            && (IsHost || pending.Candidates.Count == 1);
        _notices.Enqueue(new MultiplayerNotice.Desynced(
            pending.Turn,
            IsHost,
            mayRepair,
            $"LOCAL {ShortHash(hash ?? "unknown")}  REPORTS {pending.Details}"));
        if (!mayRepair) return;
        await CallAsync(
            token => _match.UploadSnapshotAsync(
                new UploadSnapshotRequest(
                    pending.Turn,
                    // The body is a native save, so the version that describes it is the native
                    // save format's — not the replay format's, which says nothing about these bytes.
                    NativeSaveSerializer.CurrentFormatVersion,
                    MultiplayerProtocolVersion.Current,
                    MultiplayerSessionVersion.Current,
                    hash!,
                    MatchStateClone.ToBase64(ours!),
                    SummarizeSeats(ours!)),
                token),
            _pumpLane,
            cancellationToken).ConfigureAwait(false);
    }

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
        var ours = await StateAfterTurnAsync(announced.Turn, cancellationToken).ConfigureAwait(false);
        // Compared at the DISPUTED turn, not against wherever this client is now. Delivery is at
        // least once, so the repeat of a repair already adopted arrives here — and by then several
        // turns may have been applied on top of it, which is exactly when comparing current hashes
        // says "different" and re-adopting throws all of them away.
        if (ours is not null
            && string.Equals(
                announced.StateHash,
                MatchStateHasher.ComputeSha256(ours),
                StringComparison.Ordinal))
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
    /// Every turn it passes through is re-reported, because the server waits for a report from
    /// every human seat and an earlier turn left unsettled blocks every later repair. Reports for
    /// turns already confirmed are refused with <c>turn_confirmed</c>, which
    /// <see cref="SendReportAsync"/> reads as the success it is.
    /// </para>
    /// </remarks>
    private async Task AdoptRepairAsync(SnapshotView snapshot, CancellationToken cancellationToken)
    {
        var restored = ReadVerifiedSnapshot(snapshot);
        _replay = new MatchReplayRecorder(restored);
        var settled = new List<(int Turn, string StateHash)> { (snapshot.Turn, snapshot.StateHash) };
        var view = await ReadMatchViewAsync(cancellationToken).ConfigureAwait(false);
        for (var turn = snapshot.Turn + 1; turn < view.CurrentTurn; turn++)
        {
            settled.Add((turn, await ApplySealedSetAsync(_replay, turn, cancellationToken)
                .ConfigureAwait(false)));
        }
        _pendingDesync = null;
        // Seals reconstructed before this repair are superseded by the turns just replayed.
        _unreportedSeals.Clear();
        foreach (var (turn, stateHash) in settled)
            await QueueReportAsync(turn, stateHash).WaitAsync(cancellationToken).ConfigureAwait(false);
        var current = MatchStateHasher.ComputeSha256(_replay.State);
        var (state, planning) = HandOver();
        _notices.Enqueue(new MultiplayerNotice.Resynced(snapshot.Turn, state, current, planning));
    }

    /// <summary>
    /// The state as it stood after <paramref name="turn"/>, or null when it cannot be reached.
    /// </summary>
    /// <remarks>
    /// The live state when that is where the match is, which is the common case and costs nothing.
    /// Otherwise it is rebuilt the way a reconnect rebuilds: the newest snapshot at or below the
    /// turn, then the sealed sets on top of it. Those sets are immutable and served with
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
        var baseline = await SnapshotAtOrBelowAsync(turn, cancellationToken).ConfigureAwait(false);
        if (baseline is null) return null;
        var scratch = new MatchReplayRecorder(ReadVerifiedSnapshot(baseline));
        for (var number = baseline.Turn + 1; number <= turn; number++)
            await ApplySealedSetAsync(scratch, number, cancellationToken).ConfigureAwait(false);
        return scratch.State;
    }

    /// <summary>One turn's sealed set, verified against its own digest and applied.</summary>
    private async Task<string> ApplySealedSetAsync(
        MatchReplayRecorder recorder,
        int turn,
        CancellationToken cancellationToken)
    {
        var sealedOrders = await CallAsync(
            token => _match.SealedOrdersAsync(turn, token), _pumpLane, cancellationToken)
            .ConfigureAwait(false);
        if (sealedOrders.Turn != turn)
        {
            throw new MultiplayerProtocolException(
                $"the server answered turn {turn}'s sealed set with the set for turn "
                + sealedOrders.Turn);
        }
        if (!OrderDigest.Verifies(sealedOrders, sealedOrders.OrderSetHash))
        {
            throw new MultiplayerProtocolException(
                $"the sealed set for turn {turn} does not match the digest the server announced");
        }
        return SealedTurnApplier.Apply(recorder, sealedOrders);
    }

    /// <summary>The newest snapshot no later than <paramref name="turn"/>, or null.</summary>
    /// <remarks>
    /// The latest row usually is one: while a turn is unsettled no later turn can be repaired, so
    /// nothing newer than the disputed turn can have been written. The bootstrap row is the
    /// fallback, and it is the baseline every ordinary reconnect replays from anyway.
    /// </remarks>
    private async Task<SnapshotView?> SnapshotAtOrBelowAsync(
        int turn,
        CancellationToken cancellationToken)
    {
        var latest = await LatestSnapshotOrNullAsync(cancellationToken).ConfigureAwait(false);
        if (latest is not null && latest.Turn <= turn) return latest;
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
