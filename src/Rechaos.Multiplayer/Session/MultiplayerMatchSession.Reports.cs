using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// The reporter: this client's state hash for a resolved turn, off the pump's critical path.
/// </summary>
/// <remarks>
/// <para>
/// A turn is locally safe to plan the moment its sealed set has been verified and applied, so the
/// hash leaves from here rather than from the pump, which used to wait out the whole round trip
/// before handing the player their city back.
/// </para>
/// <para>
/// In turn order, and never overtaken: the server settles one turn at a time and an earlier turn
/// left unreported blocks every later one, so the report at the head of the queue is held until it
/// is answered rather than passed over. Each request is captured when it is queued — <c>finished</c>
/// and the host's seat summaries describe the state that produced the hash, not whichever state the
/// reporter reaches when the call runs.
/// </para>
/// <para>
/// What an exhausted reconnect window means here is what it means to the outbox: the call is an
/// idempotent restatement of a hash the server either holds or is waiting for, so another window
/// opens and the player is told the connection is down. Only a refusal the server will keep
/// repeating ends the session.
/// </para>
/// </remarks>
public sealed partial class MultiplayerMatchSession
{
    /// <summary>
    /// How long a session being disposed waits for hashes it has not sent yet, by default.
    /// </summary>
    /// <remarks>
    /// Long enough for a report that is simply in flight to land, and no longer than the grace the
    /// game gives a closing window to let go of its sessions — a flush that outlasted it would be
    /// killed by the process exiting anyway, having first made a clean exit look like an abandoned
    /// one.
    /// </remarks>
    public static readonly TimeSpan DefaultReportFlushGrace = TimeSpan.FromSeconds(2);

    /// <summary>Guards the queue, the report in flight, and whether the queue is closed.</summary>
    private readonly object _reportGate = new();

    /// <summary>Wakes the reporter. Counting, so a spurious wake costs one re-check of nothing.</summary>
    private readonly SemaphoreSlim _reportSignal = new(0);

    private readonly Queue<PendingReport> _pendingReports = [];
    private readonly ConnectionHealth.Lane _reportLane;
    private readonly TimeSpan _reportFlushGrace;

    /// <summary>The report being sent, so a flush knows whether there is anything to wait for.</summary>
    private PendingReport? _reportInFlight;

    /// <summary>Set by <see cref="FlushReportsAsync"/>: an empty queue after it is the end.</summary>
    private bool _reportsClosed;

    /// <summary>
    /// Captures and queues a report for the live state in turn order.
    /// </summary>
    /// <remarks>
    /// The request is built here, before the next sealed turn can move <see cref="_replay"/>, so
    /// what is queued is a fact about one turn rather than a promise to describe the state later.
    /// </remarks>
    /// <returns>See <see cref="QueueReportAsync(TurnReport)"/>.</returns>
    private Task QueueReportAsync(int turn, string stateHash) =>
        QueueReportAsync(CaptureReport(turn, stateHash, _replay.State));

    /// <summary>
    /// The report for one turn, taken from the state that turn's resolution produced.
    /// </summary>
    /// <remarks>
    /// A reconstruction applies several turns before it reports any of them, so it captures each
    /// report as its turn is applied. Built from wherever the reconstruction ended instead, every
    /// earlier report carried the last turn's <c>finished</c> flag and seat summaries — and the
    /// server finishes a match on the first report that says it is over.
    /// </remarks>
    private TurnReport CaptureReport(int turn, string stateHash, MatchState state) => new(
        turn,
        new TurnReportRequest(
            stateHash,
            state.Outcome is not null,
            IsHost ? SummarizeSeats(state) : null));

    /// <summary>Queues a captured report behind every report queued before it.</summary>
    /// <returns>
    /// Completes when the server has answered this report, or when it has been abandoned for a
    /// reason that is not a failure. Awaited by the reconstruction paths, which must not run ahead
    /// of the settlement barrier they are trying to clear; ignored by the live path, which is the
    /// whole point of the queue.
    /// </returns>
    private Task QueueReportAsync(TurnReport captured)
    {
        if (_ownSeatIsComputerControlled) return Task.CompletedTask;
        var report = new PendingReport(captured.Turn, captured.Request);
        lock (_reportGate) _pendingReports.Enqueue(report);
        _reportSignal.Release();
        return report.Completion.Task;
    }

    private async Task RunReporterAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await _reportSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                PendingReport? report;
                lock (_reportGate)
                {
                    _pendingReports.TryDequeue(out report);
                    _reportInFlight = report;
                    // The flush wakes the reporter once with nothing behind it. An empty queue
                    // after that is everything this session had to say, said.
                    if (report is null && _reportsClosed) return;
                }
                if (report is null) continue;
                try
                {
                    await SendUntilAnsweredAsync(report, cancellationToken).ConfigureAwait(false);
                    // Whether it was sent or abandoned, nothing on this lane is still being tried.
                    // Only the attempt that succeeds clears a lane otherwise, and a report refused
                    // as already confirmed — or the last one a seat handed to the computer makes —
                    // can be the final one there will ever be, leaving the whole session reading
                    // as disconnected for the rest of the match.
                    _reportLane.Recovered();
                    report.Completion.TrySetResult();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    report.Completion.TrySetCanceled(cancellationToken);
                    throw;
                }
                catch (Exception exception)
                {
                    report.Completion.TrySetException(exception);
                    FailPendingReports(exception);
                    throw;
                }
                finally
                {
                    lock (_reportGate) _reportInFlight = null;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FailPendingReports(new OperationCanceledException(cancellationToken));
        }
        catch (Exception exception)
        {
            Fail(exception, "report_turn");
        }
    }

    /// <summary>
    /// Sends one report, opening another reconnect window each time one closes unanswered.
    /// </summary>
    /// <remarks>
    /// The answer the outbox gives its order document, for the same reason: the retry window bounds
    /// how long the client waits <em>silently</em>, not how long the match lives. Ending the session
    /// on it would mean a server restart that outlasted five minutes cost the player a match whose
    /// only outstanding business was a hash the server is still waiting to hear.
    /// </remarks>
    private async Task SendUntilAnsweredAsync(
        PendingReport report,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await SendReportAsync(report, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (RetryExhaustedException exception)
                when (TransientFailure.CanRetryAfterExhaustion(exception))
            {
                _reportLane.Failed(Describe(exception), exception.Attempts, exception);
            }
        }
    }

    /// <summary>
    /// Gives hashes that have not reached the server a bounded chance to before the session ends.
    /// </summary>
    /// <remarks>
    /// The server settles a turn only once every human seat has reported it, so a report dropped on
    /// the way out holds every other player at that turn until its deadline expires. That is worth
    /// a moment of the quitting player's time — but only a moment, and nothing is lost either way:
    /// a client that comes back replays the seal out of the history and reports it again.
    /// </remarks>
    private async Task FlushReportsAsync()
    {
        if (_reporter is not { } reporter) return;
        bool idle;
        lock (_reportGate)
        {
            _reportsClosed = true;
            idle = _reportInFlight is null && _pendingReports.Count == 0;
        }
        if (idle) return;
        _reportSignal.Release();
        try
        {
            await reporter.WaitAsync(_reportFlushGrace, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Still trying, and the player is leaving. Cancelling the session is what ends it.
        }
        catch (OperationCanceledException)
        {
            // The session was already stopping; there was nothing left to flush.
        }
    }

    private void FailPendingReports(Exception exception)
    {
        lock (_reportGate)
        {
            while (_pendingReports.TryDequeue(out var report))
                report.Completion.TrySetException(exception);
        }
    }

    /// <summary>What one turn's report says, captured from the state that turn produced.</summary>
    /// <remarks>
    /// Kept apart from <see cref="PendingReport"/> so a reconstruction that is retried queues a
    /// fresh completion rather than one an earlier attempt may already have settled.
    /// </remarks>
    private readonly record struct TurnReport(int Turn, TurnReportRequest Request);

    /// <summary>One immutable request and the completion awaited only by reconstruction paths.</summary>
    private sealed record PendingReport(int Turn, TurnReportRequest Request)
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
