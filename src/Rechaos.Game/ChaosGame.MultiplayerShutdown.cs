namespace Rechaos.Game;

/// <summary>Letting go of the online sessions when the window closes.</summary>
public sealed partial class ChaosGame
{
    /// <summary>
    /// Lets go of everything the online flow holds, as the window closes.
    /// </summary>
    /// <remarks>
    /// The sessions are given a moment to wind down rather than abandoned, because the last thing a
    /// player's own client can do for their opponents is stop the match waiting on a seat nobody is
    /// sitting in. It is a courtesy with a deadline, not a guarantee: the server's turn timer is what
    /// actually keeps a match moving when a client vanishes.
    /// </remarks>
    private void ReleaseOnlineResources()
    {
        if ((_session is not null || _lobby?.Handle is not null)
            && _activeMultiplayerRecovery is { Completed: false } recovery)
        {
            UpdateOnlineRecovery(recovery with { CleanExit = true });
        }
        CancelServerProbe();
        // The leave a player asked for on the way out is waited for too, within the same grace:
        // disposing the client under it would cut off the one request that stops the match waiting
        // on this seat.
        var stopping = new[] { _session?.StopAsync(), _lobby?.StopAsync(), _pendingLeave }
            .OfType<Task>()
            .ToArray();
        _session = null;
        _lobby = null;
        _pendingLeave = null;
        var settled = false;
        try
        {
            settled = Task.WaitAll(stopping, ShutdownGrace);
        }
        catch (AggregateException)
        {
            // A session that failed on its way out has still finished with the client.
            settled = true;
        }
        // Only once nothing is still using it. Disposing under a task that is still winding down
        // raises `ObjectDisposedException` inside that task, where nobody observes it, and the one
        // thing the grace period exists for — the `leave` that stops the match waiting on this seat
        // — is exactly the request that would be cut off. A client left undisposed at this point
        // goes with the process a moment later.
        if (settled) _http.Dispose();
        else foreach (var task in stopping) Forget(task, "multiplayer.shutdown.unfinished");
    }

    /// <summary>How long a closing window waits for the sessions to let go.</summary>
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(2);

    /// <summary>The leave request in flight after the player left a match, if any, until the window closes.</summary>
    private Task? _pendingLeave;

    /// <summary>Lets a shutdown finish on its own, logging it if it does not.</summary>
    private void Forget(Task? task, string diagnostic)
    {
        if (task is null) return;
        _ = task.ContinueWith(
            finished => _diagnostics?.Write(diagnostic, new Dictionary<string, string?>
            {
                ["error"] = RuntimeDiagnostics.ExceptionType(finished.Exception),
            }),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
