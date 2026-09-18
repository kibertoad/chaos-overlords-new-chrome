using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
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

    private Task UploadInitialSnapshotAsync(CancellationToken cancellationToken)
    {
        var stateHash = MatchStateHasher.ComputeSha256(_replay.State);
        return CallAsync(
            token => _match.UploadSnapshotAsync(
                new UploadSnapshotRequest(
                    0,
                    NativeSaveSerializer.CurrentFormatVersion,
                    stateHash,
                    MatchStateClone.ToBase64(_replay.State),
                    SummarizeSeats(_replay.State)),
                token),
            cancellationToken);
    }

}
