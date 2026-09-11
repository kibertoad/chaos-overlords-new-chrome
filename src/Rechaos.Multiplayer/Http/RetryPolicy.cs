namespace Rechaos.Multiplayer.Http;

/// <summary>How hard a failed attempt is retried before the caller gives up on it.</summary>
/// <param name="InitialDelay">First delay; it doubles up to <paramref name="MaxDelay"/>.</param>
/// <param name="MaxDelay">The ceiling on the backoff window.</param>
/// <param name="MaxAttempts">Attempts before the failure is raised, or 0 for "keep trying".</param>
public sealed record RetryPolicy(TimeSpan InitialDelay, TimeSpan MaxDelay, int MaxAttempts)
{
    /// <summary>
    /// The event stream: reconnect forever.
    /// </summary>
    /// <remarks>
    /// A match lasts as long as its players do, and a stream that gave up would leave a client
    /// silently missing turns with nothing on screen to say so. Giving up is the caller's decision,
    /// made by cancelling.
    /// </remarks>
    public static RetryPolicy Stream { get; } =
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), MaxAttempts: 0);

    /// <summary>
    /// One protocol call: a few quick attempts, then raise.
    /// </summary>
    /// <remarks>
    /// These sit between receiving a fact and acting on it, so the match is waiting on them. Five
    /// attempts over roughly ten seconds rides out a restart or a dropped connection without
    /// leaving a player staring at a turn that never resolves.
    /// </remarks>
    public static RetryPolicy Call { get; } =
        new(TimeSpan.FromMilliseconds(400), TimeSpan.FromSeconds(4), MaxAttempts: 5);

    /// <summary>
    /// Exponential with full jitter, so every client of a restarting server picks a different
    /// moment to come back rather than all of them arriving together.
    /// </summary>
    public TimeSpan Backoff(int attempt)
    {
        var doubled = InitialDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attempt - 1, 16));
        var window = Math.Min(MaxDelay.TotalMilliseconds, doubled);
        return TimeSpan.FromMilliseconds(window * (0.5 + Random.Shared.NextDouble() / 2));
    }

    /// <summary>Whether <paramref name="attempt"/> may be followed by another one.</summary>
    internal bool AllowsAnother(int attempt) => MaxAttempts <= 0 || attempt < MaxAttempts;
}

/// <summary>
/// The one definition of "worth another attempt", and the retry that acts on it.
/// </summary>
/// <remarks>
/// <para>
/// Both halves of the protocol need this and need to agree about it: the event stream reconnects,
/// and the calls a received fact leads to — fetching a sealed set, reporting a state hash, reading
/// a snapshot — have to survive the same restart the stream does. Before this was shared, a single
/// dropped connection during one of those calls ended the match.
/// </para>
/// <para>
/// Only the calls that can be repeated without changing the outcome go through here. That is every
/// read, and the two writes the protocol defines as idempotent: a report restates a hash the server
/// already holds, and an order document replaces what was held rather than adding to it.
/// </para>
/// </remarks>
public static class TransientFailure
{
    /// <summary>
    /// Whether a failure describes this attempt rather than something permanent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A refusal the server will keep repeating — a revoked token, a deleted match, a body it will
    /// never accept — is permanent; see <see cref="MultiplayerApiException.EndsTheStream"/>, which
    /// draws that line once for both callers.
    /// </para>
    /// <para>
    /// A protocol failure counts as transient because the frame or body that could not be read is
    /// one frame or body: the resume point is the last thing that did read, so trying again
    /// re-requests it rather than skipping it.
    /// </para>
    /// <para>
    /// A cancellation never does. A request that outran its own deadline arrives here as
    /// <see cref="MultiplayerTimeoutException"/> precisely so that it can be told apart from a
    /// caller that asked to stop, which must always propagate.
    /// </para>
    /// </remarks>
    public static bool IsTransient(Exception exception) => exception switch
    {
        MultiplayerApiException api => !api.EndsTheStream,
        MultiplayerTimeoutException => true,
        HttpRequestException or IOException => true,
        Protocol.MultiplayerProtocolException => true,
        _ => false,
    };

    /// <summary>
    /// Runs an idempotent call, retrying transient failures under <paramref name="policy"/>.
    /// </summary>
    /// <param name="call">The call, which may be made more than once.</param>
    /// <param name="policy">How many attempts, and how long between them.</param>
    /// <param name="onRetry">Told about each failure that will be retried, for a status line.</param>
    /// <param name="cancellationToken">Cancelling abandons the call without another attempt.</param>
    public static async Task<T> CallAsync<T>(
        Func<CancellationToken, Task<T>> call,
        RetryPolicy policy,
        Action<Exception, int>? onRetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(policy);
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await call(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
                when (IsTransient(exception) && policy.AllowsAnother(attempt))
            {
                onRetry?.Invoke(exception, attempt);
                await Task.Delay(policy.Backoff(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// A request produced nothing before its own deadline.
/// </summary>
/// <remarks>
/// This exists to keep a request that timed out distinguishable from a caller that cancelled. Both
/// surface as an <see cref="OperationCanceledException"/> from the HTTP stack, and the two need
/// opposite handling: one is worth another attempt, and the other means stop.
/// </remarks>
public sealed class MultiplayerTimeoutException(TimeSpan deadline, Exception? inner = null)
    : Exception($"the server answered nothing within {deadline.TotalSeconds:0.#}s", inner)
{
    /// <summary>The deadline the request outran.</summary>
    public TimeSpan Deadline { get; } = deadline;
}
