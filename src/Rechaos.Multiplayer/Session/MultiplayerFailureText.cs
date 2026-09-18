using System.Net;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Text a player can act on, rather than an exception's own words.
/// </summary>
/// <remarks>
/// The reason, not the status, is what this branches on: several refusals share a status and mean
/// different things (a 409 is either <c>turn_open</c> or <c>turn_not_open</c>), and the server's
/// message is written for a developer reading a log. One map for the lobby and the match, so a
/// refusal reads the same wherever it is met; the lobby upper-cases it for its screen.
/// </remarks>
public static class MultiplayerFailureText
{
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            MultiplayerApiException api when IsMembershipRevoked(api) => "You are no longer in this match.",
            MultiplayerApiException { Reason: "turn_not_open" } => "That turn has already sealed.",
            MultiplayerApiException { Reason: "unknown_match" } => "No match with that code.",
            MultiplayerApiException { Reason: "match_full" } => "That lobby is full.",
            MultiplayerApiException { Reason: "host_only" } => "Only the host can do that.",
            MultiplayerApiException { Reason: "invalid_password" } => "Wrong password.",
            MultiplayerApiException { Reason: "match_started" } => "That match has already started.",
            // A reserved display name is not in this list because it never reaches the server from
            // this client: the name is refused before a request is built, which is the only way to
            // say which field is wrong. The server refuses it too, as a contract violation like any
            // other.
            MultiplayerApiException api =>
                $"Server returned HTTP {(int)api.Status}: {api.Message}"
                + (api.RequestId is null ? string.Empty : $" (request {api.RequestId})"),
            RetryExhaustedException exhausted =>
                $"Automatic reconnect failed after {exhausted.Attempts} attempts over "
                + $"{exhausted.Elapsed.TotalMinutes:0.#} minutes. Last error: "
                + Describe(exhausted.LastError),
            MultiplayerProtocolException protocol => protocol.Message,
            MultiplayerTimeoutException timeout => timeout.Message,
            HttpRequestException request => $"Network request failed: {request.Message}",
            IOException io => $"Connection stream failed: {io.Message}",
            _ => $"{exception.GetType().Name}: {exception.Message}",
        };
    }

    /// <summary>
    /// Whether the refusal says this seat's membership is gone: the player left, or was kicked.
    /// </summary>
    /// <remarks>
    /// Terminal wherever it is met. The token is the seat, so no call made with it will ever be
    /// answered differently, and a session that kept retrying or shrugged it off as a refused
    /// document would sit in a match it is no longer part of.
    /// </remarks>
    public static bool IsMembershipRevoked(Exception exception) => exception is MultiplayerApiException
    {
        Reason: "revoked" or "invalid_token",
    } or MultiplayerApiException { Status: HttpStatusCode.Unauthorized };
}
