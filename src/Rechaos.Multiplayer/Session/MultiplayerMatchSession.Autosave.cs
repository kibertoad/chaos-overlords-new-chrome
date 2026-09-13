using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Session;

public sealed partial class MultiplayerMatchSession
{
    /// <summary>Stores the host's agreed state after every turn for crash recovery.</summary>
    private async Task AutosaveConfirmedTurnAsync(
        TurnConfirmedEvent confirmed,
        CancellationToken cancellationToken)
    {
        if (!IsHost
            || !_pendingAutosaves.Remove(confirmed.Payload.Turn, out var snapshot)
            || !string.Equals(
                snapshot.StateHash, confirmed.Payload.StateHash, StringComparison.Ordinal)) return;
        await CallAsync(
            token => _match.UploadSnapshotAsync(snapshot, token),
            cancellationToken).ConfigureAwait(false);
    }
}
