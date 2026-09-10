using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Http;

/// <summary>How hard a dropped event stream is retried before the caller gives up on it.</summary>
/// <param name="InitialDelay">First reconnect delay; it doubles up to <paramref name="MaxDelay"/>.</param>
/// <param name="MaxDelay">The ceiling on the backoff window.</param>
public sealed record ReconnectPolicy(TimeSpan InitialDelay, TimeSpan MaxDelay)
{
    public static ReconnectPolicy Default { get; } =
        new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
}

/// <summary>
/// The match's event log, forever: one connection at a time, resumed from the last sequence seen.
/// </summary>
/// <remarks>
/// <para>
/// The sequence number is the only state a client has to keep to never miss an event. Numbers are
/// gapless and allocated by the insert itself, so holding <c>seq</c> means having been offered
/// every event below it.
/// </para>
/// <para>
/// Delivery is at least once. A resume, or a seal the repair sweep finished, repeats a fact the
/// client may already hold, so every handler must be idempotent.
/// </para>
/// </remarks>
public sealed class MatchEventStream(
    MatchHandle match,
    ReconnectPolicy? policy = null,
    Action<Exception, int>? onReconnect = null)
{
    private readonly ReconnectPolicy _policy = policy ?? ReconnectPolicy.Default;
    private readonly Random _jitter = new();

    /// <summary>
    /// Events from <paramref name="afterSeq"/> onwards, reconnecting until cancelled.
    /// </summary>
    /// <remarks>
    /// A refusal the server will keep repeating — a revoked token, a deleted match — ends the
    /// stream rather than being retried forever. One that describes this attempt (a timeout, a rate
    /// limit) is retried with backoff; see <see cref="MultiplayerApiException.EndsTheStream"/>.
    /// </remarks>
    public async IAsyncEnumerable<MatchEvent> ReadAsync(
        int afterSeq,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        var after = afterSeq;
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var connection = await ConnectAsync(after, attempt + 1, cancellationToken)
                .ConfigureAwait(false);
            if (connection is not null)
            {
                await using var reader = connection;
                await using var events = reader
                    .EventsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
                while (true)
                {
                    // `MoveNextAsync` is stepped by hand rather than looped with `await foreach`
                    // because a `yield return` cannot sit inside a `try` that has a `catch`, and a
                    // connection that drops mid-stream is the ordinary case this has to recover.
                    var (moved, failure) = await StepAsync(events).ConfigureAwait(false);
                    if (failure is not null)
                    {
                        onReconnect?.Invoke(failure, attempt + 1);
                        break;
                    }
                    if (!moved) break;
                    after = Math.Max(after, events.Current.Seq);
                    attempt = 0;
                    yield return events.Current;
                }
            }
            if (cancellationToken.IsCancellationRequested) yield break;
            attempt++;
            await Task.Delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>One step of the enumerator, with a retryable failure returned rather than thrown.</summary>
    private static async Task<(bool Moved, Exception? Failure)> StepAsync(
        IAsyncEnumerator<MatchEvent> events)
    {
        try
        {
            return (await events.MoveNextAsync().ConfigureAwait(false), null);
        }
        catch (Exception exception) when (IsTransport(exception))
        {
            return (false, exception);
        }
    }

    /// <summary>
    /// One connection, or null when this attempt failed in a way worth retrying.
    /// </summary>
    /// <remarks>
    /// Connecting is separated from reading because <c>yield return</c> cannot live inside a
    /// <c>try</c> with a <c>catch</c>: the failure that ends the stream has to be raised here,
    /// where nothing is being yielded.
    /// </remarks>
    private async Task<Connection?> ConnectAsync(
        int after,
        int attempt,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await match.OpenStreamAsync(after, cancellationToken).ConfigureAwait(false);
            return new Connection(response);
        }
        catch (MultiplayerApiException exception) when (exception.EndsTheStream)
        {
            throw;
        }
        catch (Exception exception) when (IsTransport(exception))
        {
            onReconnect?.Invoke(exception, attempt);
            return null;
        }
    }

    /// <summary>
    /// A failure of this attempt rather than of the membership: worth another connection.
    /// </summary>
    /// <remarks>
    /// A protocol failure is in the list because the frame that could not be read is one frame; the
    /// resume point is the last event that did read, so reconnecting re-requests it rather than
    /// skipping it. Anything else — a cancellation, a bug — leaves the stream.
    /// </remarks>
    private static bool IsTransport(Exception exception) =>
        exception is MultiplayerApiException or HttpRequestException or IOException
            or MultiplayerProtocolException;

    /// <summary>
    /// Exponential with full jitter, so every client of a restarting server picks a different
    /// moment to come back rather than all of them arriving together.
    /// </summary>
    private TimeSpan Backoff(int attempt)
    {
        var doubled = _policy.InitialDelay.TotalMilliseconds * Math.Pow(2, Math.Min(attempt - 1, 16));
        var window = Math.Min(_policy.MaxDelay.TotalMilliseconds, doubled);
        return TimeSpan.FromMilliseconds(window * (0.5 + _jitter.NextDouble() / 2));
    }

    private sealed class Connection(HttpResponseMessage response) : IAsyncDisposable
    {
        public async IAsyncEnumerable<MatchEvent> EventsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await foreach (var @event in EventStreamParser
                .ReadAsync(body, cancellationToken).ConfigureAwait(false))
            {
                yield return @event;
            }
        }

        public ValueTask DisposeAsync()
        {
            response.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
