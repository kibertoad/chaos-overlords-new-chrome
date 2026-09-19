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
        catch (MultiplayerApiException exception) when (exception.Status is HttpStatusCode.Unauthorized
            or HttpStatusCode.NotFound)
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
}
