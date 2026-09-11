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
        string? requestId)
        : base(message)
    {
        Status = status;
        Code = code;
        Reason = reason;
        RequestId = requestId;
    }

    public HttpStatusCode Status { get; }
    public ErrorCode Code { get; }

    /// <summary>The cause from <c>details.reason</c>, e.g. <c>turn_not_open</c>. Null if absent.</summary>
    public string? Reason { get; }

    /// <summary>The server's request id, so a player can quote it from a log.</summary>
    public string? RequestId { get; }

    /// <summary>
    /// Reads the envelope out of a failed response.
    /// </summary>
    /// <remarks>
    /// A body that is not the envelope still yields a typed error: a proxy's HTML page and an empty
    /// 502 are both things a self-hosted deployment produces, and neither should surface as a
    /// parse failure on top of the failure it is reporting.
    /// </remarks>
    public static async Task<MultiplayerApiException> FromResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
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
                    error.RequestId);
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
            requestId: null);
    }

    private static async Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Whether reconnecting the event stream after this refusal could ever succeed.
    /// </summary>
    /// <remarks>
    /// Everything at 500 and above is the server having a bad moment. Below that, only a refusal
    /// about the membership itself is permanent: a timeout and a rate limit describe this attempt,
    /// and backing off is exactly the right response to both. Treating 429 as fatal would make the
    /// recovery path destroy the thing it recovers, because the reconnect loop is what spends the
    /// budget in the first place.
    /// </remarks>
    public bool EndsTheStream =>
        (int)Status < 500
        && Status is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
        // 425 Too Early has no name in HttpStatusCode. A proxy that replays requests can still
        // send it, and it says "try again", not "never".
        && (int)Status != 425;
}
