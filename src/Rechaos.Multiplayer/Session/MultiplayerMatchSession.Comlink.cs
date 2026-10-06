using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

public sealed partial class MultiplayerMatchSession
{
    /// <summary>
    /// This seat's Comlink keys: its own pair, and every other seat's published public key.
    /// </summary>
    /// <remarks>
    /// The planning copy seals what the player sends with it, and the interface opens the player's
    /// own messages with it. See docs/MULTIPLAYER.md, "Comlink privacy".
    /// </remarks>
    public ComlinkKeyring Comlink { get; }

    /// <summary>
    /// Publishes this seat's Comlink key when the roster the session started from shows another.
    /// </summary>
    /// <remarks>
    /// The lobby publishes the key as soon as the seat is taken; this is the second chance, for a
    /// publish that never reached the server and for a seat resumed with a key the server has not
    /// seen. Nothing waits on it, so it runs beside the pump on the background retry policy, and a
    /// refusal or an outage is dropped: until the key arrives, the other seats are told this one
    /// cannot receive messages yet, which is true.
    /// </remarks>
    private void PublishComlinkKeyIfStale(PlayerView self)
    {
        var publicKey = Comlink.Own.PublicKey;
        if (string.Equals(self.ComlinkKey, publicKey, StringComparison.Ordinal)) return;
        var cancellationToken = _stoppingToken;
        Forget(Task.Run(() => TryPublishComlinkKeyAsync(publicKey, cancellationToken), cancellationToken));
    }

    private async Task TryPublishComlinkKeyAsync(string publicKey, CancellationToken cancellationToken)
    {
        try
        {
            await CallAsync(
                token => _match.PublishComlinkKeyAsync(publicKey, token),
                lane: null,
                cancellationToken,
                retryPolicy: _backgroundRetryPolicy).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is MultiplayerApiException
            or MultiplayerProtocolException or RetryExhaustedException)
        {
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The session is stopping.
        }
    }
}
