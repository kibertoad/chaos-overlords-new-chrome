using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// The pump: one connection at a time, read in the order the log gives them.
/// </summary>
/// <remarks>
/// <para>
/// The log is the truth and the connection is only how it arrives. Every way a connection can end
/// that is not the session ending — the retry window closing, a sequence gap, a resync asked for
/// from outside — is answered the same way: rebuild from the server's durable history and open
/// another one. Only a refusal the server will keep repeating, or a payload this build cannot make
/// sense of, stops the session.
/// </para>
/// <para>
/// Its own file so that what a connection's life looks like reads as one piece, and so the session
/// proper stays about the match rather than about the socket.
/// </para>
/// </remarks>
public sealed partial class MultiplayerMatchSession
{
    /// <summary>The pump's whole life: reconstruct if the match moved on, then read the log forever.</summary>
    private async Task RunPumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (IsRestoring && !await RestoreAsync(replayFromSeq: 0, cancellationToken).ConfigureAwait(false))
                return;
            await PumpAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ordinary shutdown, or the outbox failed first and stopped the session.
        }
        catch (Exception exception)
        {
            Fail(exception, Volatile.Read(ref _pumpOperation) ?? "event_stream");
        }
    }

    /// <summary>
    /// Reads the log forever, acting on every fact in the order the log gives them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A failure that ends the stream ends the session with it: a revoked token means the player
    /// left or was kicked, and there is nothing left to read. Everything that describes one attempt
    /// is retried — the stream by itself, and the calls a fact leads to by <see cref="CallAsync"/>.
    /// </para>
    /// <para>
    /// Sequence numbers are gapless, so the stream is held to that: an event that skips past the
    /// next expected number means something between was never delivered, and acting on what came
    /// after would apply a later turn to an earlier state. The session resynchronises instead — the
    /// same reconstruction a restart does — and resumes from where the server now is. An event at
    /// or below the last applied number is the at-least-once repeat and is dropped.
    /// </para>
    /// </remarks>
    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        if (_uploadInitialSnapshot)
        {
            await UploadInitialSnapshotAsync(cancellationToken).ConfigureAwait(false);
            _uploadInitialSnapshot = false;
        }
        // How long the stream has been down across consecutive windows, reset by any connection
        // that opens. Only `_streamOutageBudget` ends the session over it; see the option.
        var outage = new System.Diagnostics.Stopwatch();
        var attemptsThisOutage = 0;
        while (true)
        {
            Volatile.Write(ref _pumpOperation, "event_stream");
            using var cycle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Volatile.Write(ref _streamCycle, cycle);
            var restart = false;
            try
            {
                restart = await ReadStreamCycleAsync(outage, cycle.Token).ConfigureAwait(false);
            }
            catch (RetryExhaustedException exhausted)
                when (TransientFailure.CanRetryAfterExhaustion(exhausted))
            {
                // The retry window is the bound on SILENT waiting, not on the session. The outbox
                // on this same session has always treated it that way — it requeues the draft and
                // opens another window — and the stream ending the session here meant a six-minute
                // sleep cost the player their city screen while their orders survived.
                if (!outage.IsRunning) outage.Restart();
                attemptsThisOutage += exhausted.Attempts;
                if (_streamOutageBudget is { } budget && outage.Elapsed >= budget) throw;
                _streamLane.Failed(
                    $"Still trying to reach the server, {outage.Elapsed.TotalMinutes:0.#} minutes "
                    + "so far. The match resumes as soon as it answers.",
                    attemptsThisOutage);
                restart = true;
            }
            catch (OperationCanceledException)
                when (cycle.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // A resync was asked for from outside; see `RequestResync`.
                restart = true;
            }
            Volatile.Write(ref _streamCycle, null);
            // The stream ends only by throwing, by cancellation, or by a restart above.
            if (!restart) return;
            if (Interlocked.Exchange(ref _resyncRequested, 0) != 0) outage.Reset();
            if (!await RestoreAsync(_resumeAfterSeq, cancellationToken).ConfigureAwait(false)) return;
            outage.Reset();
            attemptsThisOutage = 0;
            _streamLane.Recovered();
        }
    }

    /// <summary>
    /// One connection's worth of reading. True when the pump should rebuild and open another.
    /// </summary>
    private async Task<bool> ReadStreamCycleAsync(
        System.Diagnostics.Stopwatch outage,
        CancellationToken cancellationToken)
    {
        var stream = new MatchEventStream(
            _match,
            _streamRetryPolicy,
            onReconnect: (exception, attempt) =>
            {
                if (!outage.IsRunning) outage.Restart();
                _streamLane.Failed(Describe(exception), attempt);
            },
            onConnected: () =>
            {
                outage.Reset();
                _streamLane.Recovered();
            },
            _streamIdleTimeout);
        await foreach (var @event in stream
            .ReadAsync(_resumeAfterSeq, cancellationToken).ConfigureAwait(false))
        {
            if (!string.Equals(@event.MatchId, _match.MatchId, StringComparison.Ordinal))
            {
                throw new MultiplayerProtocolException(
                    $"event sequence {@event.Seq} belongs to another match");
            }
            var expected = _resumeAfterSeq + 1;
            if (@event.Seq < expected) continue;
            if (@event.Seq > expected)
            {
                _streamLane.Failed(
                    $"The event stream jumped from sequence {_resumeAfterSeq} to {@event.Seq}; "
                    + "resynchronising with the server.",
                    attempt: 1);
                return true;
            }
            await HandleAsync(@event, cancellationToken).ConfigureAwait(false);
            _resumeAfterSeq = @event.Seq;
        }
        return false;
    }
}
