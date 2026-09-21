using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Session;

/// <summary>Reporting resolved-turn hashes without making the player wait for their acknowledgement.</summary>
public sealed partial class MultiplayerMatchSession
{
    private readonly object _reportGate = new();
    private readonly SemaphoreSlim _reportSignal = new(0);
    private readonly Queue<PendingReport> _pendingReports = [];
    private readonly ConnectionHealth.Lane _reportLane;

    /// <summary>
    /// Captures and queues a report in turn order.
    /// </summary>
    /// <remarks>
    /// The request is built before the next sealed turn can move <see cref="_replay"/>. In
    /// particular, <c>finished</c> and the host's seat summaries describe the state that produced
    /// this hash, not whichever state the reporter happens to reach when the HTTP call runs.
    /// </remarks>
    private Task QueueReportAsync(int turn, string stateHash)
    {
        if (_ownSeatIsComputerControlled) return Task.CompletedTask;
        var report = new PendingReport(
            turn,
            new TurnReportRequest(
                stateHash,
                _replay.State.Outcome is not null,
                IsHost ? SummarizeSeats(_replay.State) : null));
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
                PendingReport report;
                lock (_reportGate) report = _pendingReports.Dequeue();
                try
                {
                    await SendReportAsync(report, cancellationToken).ConfigureAwait(false);
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

    private void FailPendingReports(Exception exception)
    {
        lock (_reportGate)
        {
            while (_pendingReports.TryDequeue(out var report))
                report.Completion.TrySetException(exception);
        }
    }

    /// <summary>One immutable request and the completion awaited only by reconstruction paths.</summary>
    private sealed record PendingReport(int Turn, TurnReportRequest Request)
    {
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
