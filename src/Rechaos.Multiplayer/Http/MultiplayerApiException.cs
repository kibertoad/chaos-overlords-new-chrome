using System.Net;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Http;

/// <summary>A refusal from the server, with the envelope's machine-readable cause.</summary>
/// <remarks>
/// The reason, not the status, is what a caller branches on: several refusals share a status and
/// mean different things (a 409 is either <c>turn_open</c> or <c>turn_not_open</c>), and the
/// message is for a player to read, not for code.
/// </remarks>
public sealed class MultiplayerApiException : Exception
{
    private MultiplayerApiException(
        HttpStatusCode status,
        ErrorCode code,
        string message,
        string? reason,
        string? requestId,
        bool fromEnvelope,
        TimeSpan? retryAfter)
        : base(message)
    {
        Status = status;
        Code = code;
        Reason = reason;
        RequestId = requestId;
        FromEnvelope = fromEnvelope;
        RetryAfter = retryAfter;
    }

    public HttpStatusCode Status { get; }
    public ErrorCode Code { get; }

    /// <summary>The cause from <c>details.reason</c>, e.g. <c>turn_not_open</c>. Null if absent.</summary>
    public string? Reason { get; }

    /// <summary>The server's request id, so a player can quote it from a log.</summary>
    public string? RequestId { get; }

    /// <summary>
    /// Whether the body was this server's own error envelope, rather than a status stood in for one.
    /// </summary>
    /// <remarks>
    /// The distinction decides whether a refusal is permanent. Everything between the player and the
    /// coordination server can answer a 4xx of its own: a reverse proxy that serves 404 for every
    /// path while the backend restarts, a tunnel that answers 403 once its session lapses, a captive
    /// portal, another service that took the port. None of those say anything about the match, and a
    /// session that read them as the server's verdict ended over an outage it would have ridden out.
    /// An envelope, by contrast, came from the server's own error handler and means what it says.
    /// </remarks>
    public bool FromEnvelope { get; }

    /// <summary>
    /// How long the server asked the caller to wait before trying again, from <c>Retry-After</c>.
    /// </summary>
    /// <remarks>
    /// Sent with every rate-limit refusal. The server's limiter works in fixed windows, so an
    /// attempt made before the window turns over is refused again for nothing — and each one was
    /// another line in the reconnect log. Null when the header is absent or unreadable.
    /// </remarks>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Whether this is the server limiting how often this client may call it.</summary>
    public bool IsRateLimited => Status == HttpStatusCode.TooManyRequests;

    /// <summary>
    /// The most of a refusal's body that is read before it is judged not to be an envelope.
    /// </summary>
    /// <remarks>
    /// An envelope is a few hundred bytes; the largest the server writes is a validation refusal
    /// listing every issue in an order document, which stays well inside a megabyte. A body past
    /// this is not the server's error handler talking, and buffering it would be this process
    /// holding whatever a proxy — or something posing as the server — decided to send.
    /// </remarks>
    public const int MaximumBodyBytes = 1024 * 1024;

    /// <summary>
    /// Reads the envelope out of a failed response.
    /// </summary>
    /// <remarks>
    /// A body that is not the envelope still yields a typed error: a proxy's HTML page and an empty
    /// 502 are both things a self-hosted deployment produces, and neither should surface as a
    /// parse failure on top of the failure it is reporting. A body over
    /// <see cref="MaximumBodyBytes"/>, or one the transport loses, is treated the same way.
    /// </remarks>
    public static Task<MultiplayerApiException> FromResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        FromResponseAsync(response, Timeout.InfiniteTimeSpan, cancellationToken);

    /// <inheritdoc cref="FromResponseAsync(HttpResponseMessage, CancellationToken)"/>
    /// <param name="response">The refusal.</param>
    /// <param name="bodyTimeout">
    /// How long the body may take once the status is in; zero or less waits for as long as the
    /// caller does. A body that outruns it becomes a status-only refusal: the status did arrive, and
    /// throwing a timeout instead would both lose it and claim the server said nothing.
    /// </param>
    /// <param name="cancellationToken">The caller's; cancelling it still throws.</param>
    internal static async Task<MultiplayerApiException> FromResponseAsync(
        HttpResponseMessage response,
        TimeSpan bodyTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var headerRequestId = response.Headers.TryGetValues("X-Request-Id", out var requestIds)
            ? requestIds.FirstOrDefault()
            : null;
        var retryAfter = RetryAfterOf(response);
        var body = await ReadBodyAsync(response, bodyTimeout, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            // Read tolerantly. The reason is what a caller branches on and what a player is shown,
            // so an envelope carrying a field this build does not know about must not cost us both.
            // The null check is not paranoia: any JSON object parses as this record with `error`
            // left unset, so a proxy that answers `{}` — or any JSON at all that is not the envelope
            // — arrives here as a shape that is syntactically fine and has nothing in it. Reaching
            // into it would raise a NullReferenceException from inside the code whose whole job is
            // to turn a failure into something a caller can read.
            if (WireJson.Read<ErrorEnvelope>(body) is { Error: { } error })
            {
                return new MultiplayerApiException(
                    response.StatusCode,
                    error.Code,
                    error.Message,
                    error.Details?.Reason,
                    error.RequestId ?? headerRequestId,
                    fromEnvelope: true,
                    retryAfter);
            }
        }
        catch (MultiplayerProtocolException)
        {
            // Not JSON at all. A proxy's HTML page and an empty 502 both land here; fall through to
            // the status-only error below.
        }
        return new MultiplayerApiException(
            response.StatusCode,
            ErrorCode.Internal,
            $"HTTP {(int)response.StatusCode}",
            reason: null,
            requestId: headerRequestId,
            fromEnvelope: false,
            retryAfter);
    }

    /// <summary>The wait <c>Retry-After</c> asks for, as a delay or a date; null when it says neither.</summary>
    private static TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        var wait = header?.Delta
            ?? (header?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        return wait is { } value ? (value > TimeSpan.Zero ? value : TimeSpan.Zero) : null;
    }

    /// <summary>
    /// The body, or empty when it cannot be the envelope: too large, too slow, or cut off.
    /// </summary>
    private static async Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > TimeSpan.Zero) deadline.CancelAfter(timeout);
        try
        {
            return await BoundedBody
                .ReadStringAsync(response.Content, MaximumBodyBytes, deadline.Token)
                .ConfigureAwait(false) ?? string.Empty;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException
            || (exception is OperationCanceledException
                && !cancellationToken.IsCancellationRequested))
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Whether reconnecting the event stream after this refusal could ever succeed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything at 500 and above is the server having a bad moment. Below that, only a refusal
    /// about the membership itself is permanent: a timeout and a rate limit describe this attempt,
    /// and backing off is exactly the right response to both. Treating 429 as fatal would make the
    /// recovery path destroy the thing it recovers, because the reconnect loop is what spends the
    /// budget in the first place.
    /// </para>
    /// <para>
    /// A 4xx that did not come with an envelope is transient, whatever its number. It was not this
    /// server's verdict: <see cref="FromResponseAsync"/> deliberately builds one of these out of a
    /// proxy's HTML page or an empty body, so a reverse proxy answering 404 or 403 for every path
    /// while the backend restarts used to end a session the client could simply have waited out.
    /// This is the rule the recovery file already applies to the same question.
    /// </para>
    /// </remarks>
    public bool EndsTheStream =>
        FromEnvelope
        && (int)Status < 500
        && Status is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
        // 425 Too Early has no name in HttpStatusCode. A proxy that replays requests can still
        // send it, and it says "try again", not "never".
        && (int)Status != 425;
}
