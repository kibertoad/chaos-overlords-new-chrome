using System.Net;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Game;

/// <summary>Checks whether saved memberships still name a match the coordination server retains.</summary>
/// <remarks>
/// A recovery file is local history, not an authority on a server's retention policy.  Keeping an
/// entry after its server has deleted the match only offers the player a reconnect that cannot work.
/// Transport failures deliberately keep the entry: an unreachable server says nothing about whether
/// it still has the match.
/// </remarks>
public static class MultiplayerRecoveryReconciliation
{
    /// <summary>
    /// Returns the saved memberships the server has conclusively retired or revoked.
    /// </summary>
    public static async Task<IReadOnlyList<MultiplayerRecovery>> FindUnavailableAsync(
        HttpClient http,
        IEnumerable<MultiplayerRecovery> recoveries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(recoveries);
        var checks = recoveries.Select(recovery => CheckAsync(http, recovery, cancellationToken));
        var results = await Task.WhenAll(checks).ConfigureAwait(false);
        return results.Where(recovery => recovery is not null).Select(recovery => recovery!).ToArray();
    }

    private static async Task<MultiplayerRecovery?> CheckAsync(
        HttpClient http,
        MultiplayerRecovery recovery,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(recovery.Server, UriKind.Absolute, out var server)) return recovery;
        try
        {
            await new MultiplayerClient(http, new MultiplayerClientOptions(server))
                .WithToken(recovery.Token)
                .Match(recovery.MatchId)
                .GetAsync(cancellationToken)
                .ConfigureAwait(false);
            return null;
        }
        catch (MultiplayerApiException exception) when (IsMembershipGone(exception))
        {
            // A deleted match invalidates its token, and a revoked token cannot be used to replay
            // anything either. Both are definitive; every other failure might be transient.
            return recovery;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is MultiplayerApiException
            or MultiplayerProtocolException or MultiplayerTimeoutException
            or HttpRequestException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the refusal is the server saying this membership is gone.
    /// </summary>
    /// <remarks>
    /// The status alone is not enough. <see cref="MultiplayerApiException.FromResponseAsync"/>
    /// builds one of these for any unsuccessful response, so a reverse proxy answering 404 for
    /// every path while the server behind it is down — or another service listening on that port —
    /// produced a 404 with no error envelope, and the player's seat in a running match was deleted
    /// from the file for good. A refusal that names a membership reason came from this server's own
    /// error handler and is definitive; anything else might be transient.
    /// </remarks>
    private static bool IsMembershipGone(MultiplayerApiException exception) =>
        exception.Status is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound
        && exception.Reason is "unknown_match" or "unknown_player" or "invalid_token"
            or "missing_token";
}
