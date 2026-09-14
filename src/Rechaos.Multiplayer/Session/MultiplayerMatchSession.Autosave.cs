using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Session;

public sealed partial class MultiplayerMatchSession
{
    private static IReadOnlyList<AiSeatSummary> SummarizeSeats(MatchState state) =>
        state.Players.Select(player => new AiSeatSummary(
            player.Id.Value,
            player.Gangs.Count(gang => gang.IsActive),
            state.Sectors.Sum(sector => sector.Sites.Count(site => site.InfluencedBy == player.Id)),
            state.Sectors.Count(sector => sector.Owner == player.Id))).ToArray();

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
