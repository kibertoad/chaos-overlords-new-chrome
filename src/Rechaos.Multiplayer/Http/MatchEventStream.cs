using Rechaos.Multiplayer.Generated;

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
/// <para>
/// A connection counts as made when it has carried an event or outlived a heartbeat (see
/// <see cref="KeepalivesToProve"/>), not when the server accepts it. A server that accepts and
/// then closes, or accepts and then says nothing, would otherwise reset the retry budget on every
/// attempt and be reconnected to forever; and the interface would be told the connection was back
/// before anything had come down it.
/// </para>
/// </remarks>
/// <param name="match">The token-bound handle the stream is opened through.</param>
/// <param name="policy">How long to keep reconnecting; <see cref="RetryPolicy.Stream"/> by default.</param>
/// <param name="onReconnect">Told about each failure that will be retried, with the attempt number.</param>
/// <param name="onConnected">Told when a connection has proven itself by delivering a frame.</param>
/// <param name="idleTimeout">
/// How long a connection may carry nothing before it is dropped and reopened, or null for
/// <see cref="DefaultIdleTimeout"/>. The server heartbeats every <see cref="ServerHeartbeat"/>.
/// Only time spent waiting on the connection counts; time the consumer spends on an event it was
/// given is not silence.
/// </param>
public sealed class MatchEventStream(
    MatchHandle match,
    RetryPolicy? policy = null,
    Action<Exception, int>? onReconnect = null,
    Action? onConnected = null,
    TimeSpan? idleTimeout = null)
{
    /// <summary>How often the server writes a keepalive comment into an open stream.</summary>
    public static readonly TimeSpan ServerHeartbeat = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Two and a half heartbeats: one may be late and one may be lost before silence means anything.
    /// </summary>
    public static readonly TimeSpan DefaultIdleTimeout = ServerHeartbeat * 2.5;

    /// <summary>
    /// How many data-less frames a connection that has carried no event must deliver before it
    /// counts as one: the server's <c>: connected</c> comment, then a heartbeat keepalive.
    /// </summary>
    /// <remarks>
    /// The first frame of any kind used to be enough, which made a whole class of broken middlebox
    /// invisible to the backoff: a proxy with response buffering, or a load balancer with a short
    /// idle cutoff, accepts the stream, writes the server's <c>: connected</c> comment and drops
    /// it. Every such attempt was "proven", so the attempt counter reset to zero and the outage
    /// stopwatch never started — and the client reconnected at the first backoff step, half a
    /// second to a second apart, for as long as the player left the match open. The design says
    /// the opposite: a server that accepts the connection and closes it at once is an outage like
    /// any other.
    ///
    /// The server opens every stream with <c>: connected</c> and then writes a keepalive every
    /// <see cref="ServerHeartbeat"/>, so a second data-less frame means the connection outlived a
    /// heartbeat; and any real EVENT proves it immediately, whenever it arrives.
    ///
    /// This is counted rather than timed. A stopwatch here starts only once the response headers
    /// have arrived, after the server has already started its heartbeat timer, so the first
    /// keepalive lands short of a full heartbeat on the client's clock by however long the headers
    /// took. A threshold of one heartbeat missed it on every reconnect and held the "connection
    /// lost" dialog up for some forty seconds over a stream that had been working since the first;
    /// shaving the threshold only moved the problem to a slow TLS handshake, proxy or cold start.
    /// The count does not depend on either clock, nor on the server's heartbeat interval.
    /// </remarks>
    public const int KeepalivesToProve = 2;

    private readonly RetryPolicy _policy = policy ?? RetryPolicy.Stream;
    private readonly TimeSpan _idleTimeout = idleTimeout ?? DefaultIdleTimeout;

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
        var outage = new System.Diagnostics.Stopwatch();
        Exception? lastFailure = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            // Establish the protocol again on every fresh connection. A stream that reconnects
            // through a redeploy is exactly the case where the server on the other end may no
            // longer be the one this session handshook with, and every call made after it would go
            // out under a contract neither side has agreed to; see `ForgetHandshake`.
            if (attempt > 0) match.ForgetHandshake();
            var connected = await ConnectAsync(after, cancellationToken).ConfigureAwait(false);
            if (connected.Connection is not null)
            {
                await using var reader = connected.Connection;
                await using var frames = reader
                    .FramesAsync(_idleTimeout, cancellationToken).GetAsyncEnumerator(cancellationToken);
                // A connection has to LAST to count as one; see `KeepalivesToProve`.
                var keepalives = 0;
                var proven = false;
                while (true)
                {
                    // `MoveNextAsync` is stepped by hand rather than looped with `await foreach`
                    // because a `yield return` cannot sit inside a `try` that has a `catch`, and a
                    // connection that drops mid-stream is the ordinary case this has to recover.
                    var (moved, failure) = await StepAsync(frames).ConfigureAwait(false);
                    if (!moved) failure ??= new IOException("the server closed the event stream");
                    if (failure is not null)
                    {
                        lastFailure = failure;
                        onReconnect?.Invoke(failure, attempt + 1);
                        if (!outage.IsRunning) outage.Start();
                        break;
                    }
                    if (frames.Current.IsKeepalive) keepalives++;
                    if (!proven && (!frames.Current.IsKeepalive || keepalives >= KeepalivesToProve))
                    {
                        // An EVENT proves the connection at once — the server is talking, and the
                        // client is reading a live match. The opening comment alone does not: see
                        // `KeepalivesToProve` for what that used to cost.
                        proven = true;
                        attempt = 0;
                        outage.Reset();
                        onConnected?.Invoke();
                    }
                    if (frames.Current.Event is not { } @event) continue;
                    after = Math.Max(after, @event.Seq);
                    yield return @event;
                }
            }
            else if (connected.Failure is { } failure)
            {
                lastFailure = failure;
                onReconnect?.Invoke(failure, attempt + 1);
                if (!outage.IsRunning) outage.Start();
            }
            if (cancellationToken.IsCancellationRequested) yield break;
            attempt++;
            if (_policy.NextDelay(lastFailure, attempt, outage.Elapsed) is not { } delay)
                throw new RetryExhaustedException(
                    attempt,
                    outage.Elapsed,
                    lastFailure ?? new IOException("the server event stream did not reconnect"));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>One step of the enumerator, with a retryable failure returned rather than thrown.</summary>
    private static async Task<(bool Moved, Exception? Failure)> StepAsync(
        IAsyncEnumerator<EventStreamFrame> frames)
    {
        try
        {
            return (await frames.MoveNextAsync().ConfigureAwait(false), null);
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
    private async Task<(Connection? Connection, Exception? Failure)> ConnectAsync(
        int after,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await match.OpenStreamAsync(after, cancellationToken).ConfigureAwait(false);
            return (new Connection(response), null);
        }
        catch (Exception exception) when (TransientFailure.IsTransient(exception))
        {
            return (null, exception);
        }
    }

    private sealed class Connection(HttpResponseMessage response) : IAsyncDisposable
    {
        public async IAsyncEnumerable<EventStreamFrame> FramesAsync(
            TimeSpan idleTimeout,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await foreach (var frame in EventStreamParser
                .ReadFramesAsync(body, idleTimeout, cancellationToken).ConfigureAwait(false))
            {
                yield return frame;
            }
        }

        public ValueTask DisposeAsync()
        {
            response.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
