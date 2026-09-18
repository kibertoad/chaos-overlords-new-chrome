namespace Rechaos.Multiplayer.Http;

/// <summary>How hard a failed attempt is retried before the caller gives up on it.</summary>
/// <param name="InitialDelay">First delay; it doubles up to <paramref name="MaxDelay"/>.</param>
/// <param name="MaxDelay">The ceiling on the backoff window.</param>
/// <param name="MaxAttempts">Attempts before the failure is raised, or 0 for no count limit.</param>
/// <param name="MaxElapsed">Retry window, or null for no time limit.</param>
public sealed record RetryPolicy(
    TimeSpan InitialDelay,
    TimeSpan MaxDelay,
    int MaxAttempts,
    TimeSpan? MaxElapsed = null)
{
    /// <summary>
    /// The event stream: reconnect transient failures for five minutes.
    /// </summary>
    /// <remarks>
    /// Five minutes covers a server restart or ordinary connection outage without leaving a client
    /// silently missing turns forever. The interface also lets the player cancel this wait early.
    /// </remarks>
    public static RetryPolicy Stream { get; } =
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15), MaxAttempts: 0,
            MaxElapsed: TimeSpan.FromMinutes(5));

    /// <summary>
    /// One idempotent protocol call: keep trying transient failures for five minutes.
    /// </summary>
    /// <remarks>
    /// These sit between receiving a fact and acting on it, so the match is waiting on them. A
    /// server restart or temporary network outage must not end the session after a few seconds;
    /// five minutes gives the connection time to return while still surfacing a prolonged outage.
    /// </remarks>
    public static RetryPolicy Call { get; } =
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15), MaxAttempts: 0,
            MaxElapsed: TimeSpan.FromMinutes(5));

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
    internal bool AllowsAnother(int attempt, TimeSpan elapsed) =>
        (MaxAttempts <= 0 || attempt < MaxAttempts)
        && (MaxElapsed is null || elapsed < MaxElapsed.Value);
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
    /// A protocol failure is permanent for this build: retrying the same malformed or incompatible
    /// body for five minutes cannot make it readable and would hide the useful diagnosis.
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
        var started = System.Diagnostics.Stopwatch.StartNew();
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await call(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                if (!policy.AllowsAnother(attempt, started.Elapsed))
                    throw new RetryExhaustedException(attempt, started.Elapsed, exception);
                onRetry?.Invoke(exception, attempt);
                await Task.Delay(policy.Backoff(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

/// <summary>A transient operation still failed after its complete reconnect window.</summary>
public sealed class RetryExhaustedException(
    int attempts,
    TimeSpan elapsed,
    Exception lastError)
    : Exception(
        $"automatic reconnect failed after {attempts} attempts over {elapsed.TotalMinutes:0.#} minutes: "
        + lastError.Message,
        lastError)
{
    public int Attempts { get; } = attempts;
    public TimeSpan Elapsed { get; } = elapsed;
    public Exception LastError { get; } = lastError;
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
