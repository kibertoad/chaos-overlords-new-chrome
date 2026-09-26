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
            MultiplayerApiException { Reason: "not_active" } =>
                "Your seat is not being played by you at the moment.",
            MultiplayerApiException { Reason: "unknown_match" } => "No match with that code.",
            MultiplayerApiException { Reason: "match_full" } => "That lobby is full.",
            MultiplayerApiException { Reason: "host_only" } => "Only the host can do that.",
            // The codes below are the kernel's own (`LobbyService`), spelled as it spells them: a
            // map written against names the server never sends falls through to the raw HTTP line.
            MultiplayerApiException { Reason: "unknown_join_code" } => "No lobby is open with that code.",
            MultiplayerApiException { Reason: "password_required" } => "That match needs a password.",
            MultiplayerApiException { Reason: "wrong_password" } => "Wrong password.",
            // `match_not_in_lobby` refuses a lobby action on a started match; `match_not_joinable`
            // is a join that lost the race with the host pressing start.
            MultiplayerApiException { Reason: "match_not_in_lobby" or "match_not_joinable" } =>
                "That match has already started.",
            // Only ever about the player a kick or a takeover vote NAMED, never the caller — whose
            // own membership going away answers `invalid_token` — so it is not a membership reason.
            MultiplayerApiException { Reason: "unknown_player" } =>
                "That player is no longer in this match.",
            MultiplayerApiException { Reason: "unreadable_event" } =>
                "The server holds a match event it cannot read, so this match cannot be resumed.",
            // A reserved display name is not in this list because it never reaches the server from
            // this client: the name is refused before a request is built, which is the only way to
            // say which field is wrong. The server refuses it too, as a contract violation like any
            // other.
            MultiplayerApiException { IsRateLimited: true } api => RateLimited(api),
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
    /// Whether the failure is the server limiting how often this client may call it.
    /// </summary>
    /// <remarks>
    /// Not a lost connection: the server answered, and it will answer again once the window turns
    /// over. The interface says so rather than telling the player their network is down.
    /// </remarks>
    public static bool IsRateLimited(Exception? exception) =>
        MultiplayerApiException.AnswerIn(exception)?.IsRateLimited == true;

    private static string RateLimited(MultiplayerApiException api)
    {
        var wait = api.RetryAfter is { } retryAfter
            ? $"; it asked to wait {Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))} s"
            : string.Empty;
        return $"The server is limiting how often this client may call it{wait}."
            + (api.RequestId is null ? string.Empty : $" (request {api.RequestId})");
    }

    /// <summary>
    /// Reasons this server's own error handler uses when a seat's membership is gone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named here so the stream, the outbox and the recovery file all answer the question the same
    /// way. Each is written by exactly one place in the kernel: the token no longer resolves, the
    /// match it named is gone, or the seat is no longer one this player holds.
    /// </para>
    /// <para>
    /// <c>not_active</c> is deliberately NOT one of them. It says the seat is not the one the
    /// server is taking this write from right now — it was voted onto computer control while the
    /// player slept, or they left — and the kernel's own <c>rejoin</c> turns away nobody but the
    /// kicked, whose token is revoked anyway. Counting it here ended the session in the outbox and
    /// deleted a live seat from the recovery file, while <c>SendReportAsync</c> swallowed the same 403
    /// and kept the client watching the match: the two paths disagreed about whether the player
    /// was still there.
    /// </para>
    /// <para>
    /// Only codes the kernel really sends. <c>revoked</c> was listed and is written nowhere: a
    /// removed seat's token stops resolving, which is <c>invalid_token</c>. <c>unknown_player</c>
    /// was listed and names the TARGET of a kick or a takeover vote that is not in the match, so a
    /// vote on a seat that had just gone ended the voter's own session. <c>kicked</c> is the
    /// kernel's answer to a rejoin by a kicked player that races the token's revocation.
    /// </para>
    /// </remarks>
    private static readonly string[] MembershipReasons =
    [
        "invalid_token", "missing_token", "unknown_match", "kicked",
    ];

    /// <summary>
    /// Whether the refusal says this seat's membership is gone: the player left, or was kicked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Terminal wherever it is met. The token is the seat, so no call made with it will ever be
    /// answered differently, and a session that kept retrying or shrugged it off as a refused
    /// document would sit in a match it is no longer part of.
    /// </para>
    /// <para>
    /// It has to be the server saying so. Any 401 used to count, and a 401 is what a reverse proxy
    /// whose own credentials lapsed, or a tunnel that has gone stale, answers for every path — so a
    /// player was told they had been removed from a match they were still in, and the session ended.
    /// A refusal that names one of <see cref="MembershipReasons"/> came from this server's own error
    /// handler; anything else might be transient, which is the rule
    /// <c>MultiplayerRecoveryReconciliation</c> already states for the recovery file.
    /// </para>
    /// </remarks>
    public static bool IsMembershipRevoked(Exception exception) =>
        exception is MultiplayerApiException { FromEnvelope: true, Reason: { } reason }
        && Array.IndexOf(MembershipReasons, reason) >= 0;
}
