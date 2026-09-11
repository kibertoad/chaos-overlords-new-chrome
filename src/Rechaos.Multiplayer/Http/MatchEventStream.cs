using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Http;

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
    RetryPolicy? policy = null,
    Action<Exception, int>? onReconnect = null,
    Action? onConnected = null)
{
    private readonly RetryPolicy _policy = policy ?? RetryPolicy.Stream;

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
                onConnected?.Invoke();
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
            await Task.Delay(_policy.Backoff(attempt), cancellationToken).ConfigureAwait(false);
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
        catch (Exception exception) when (TransientFailure.IsTransient(exception))
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
        catch (Exception exception) when (TransientFailure.IsTransient(exception))
        {
            onReconnect?.Invoke(exception, attempt);
            return null;
        }
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
