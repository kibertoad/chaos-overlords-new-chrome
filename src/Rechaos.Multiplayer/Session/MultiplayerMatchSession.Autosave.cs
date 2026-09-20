using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

public sealed partial class MultiplayerMatchSession
{
    private static IReadOnlyList<AiSeatSummary> SummarizeSeats(MatchState state) =>
        state.Players.Select(player => new AiSeatSummary(
            player.Id.Value,
            player.Gangs.Count(gang => gang.IsActive),
            state.Sectors.Sum(sector => sector.Sites.Count(site => site.InfluencedBy == player.Id)),
            state.Sectors.Count(sector => sector.Owner == player.Id))).ToArray();

    /// <summary>
    /// Confirmed turns between two checkpoints.
    /// </summary>
    /// <remarks>
    /// A reconnect replays from the newest snapshot the server holds, one sequential sealed-set
    /// fetch and one full turn resolution per turn since. With only the turn-0 bootstrap and desync
    /// repairs to replay from, a reconnect at turn 80 was eighty of each — minutes of it, inside
    /// the pump, before the player saw anything, and every retry started over. The design doc talks
    /// about matches of hundreds of turns.
    ///
    /// Ten bounds that at ten turns' replay. The cost is one upload of a megabyte every ten turns,
    /// by the host only, on a path the player is not waiting on; the server keeps five snapshots
    /// per match and prunes the rest, so what it holds does not grow either.
    /// </remarks>
    private const int CheckpointEveryTurns = 10;

    /// <summary>
    /// Writes the host's turn-0 bootstrap snapshot, if this client still owes one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is worth sending: it opens the late-join door and it is the oldest thing a reconnect can
    /// replay from. But nothing on this client is waiting for it, and the window the server accepts
    /// it in can close while this client is standing in it — a turn-0 upload is a bootstrap only
    /// while the match is on turn 1, and once turn 1 seals the same request is answered
    /// <c>unknown_turn</c>. Ending the session on that would cost the player the match to save the
    /// next reconnect a few seconds, so it is treated like a checkpoint: try, and carry on.
    /// </para>
    /// <para>
    /// Nothing is lost by carrying on. A restore re-arms this while the match is still on turn 1,
    /// and once it is not, a checkpoint is what late join and the next reconnect read instead.
    /// </para>
    /// </remarks>
    private async Task UploadBootstrapSnapshotIfDueAsync(CancellationToken cancellationToken)
    {
        if (!_uploadInitialSnapshot) return;
        _uploadInitialSnapshot = false;
        try
        {
            await UploadSnapshotAsync(
                    0, MatchStateHasher.ComputeSha256(_replay.State), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is MultiplayerApiException
            or MultiplayerProtocolException or RetryExhaustedException)
        {
            // See above: a bootstrap nobody is waiting on.
        }
    }

    /// <summary>
    /// Writes a checkpoint of a turn the match has just confirmed, if one is due.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host only, and only for the turn this client has just finished applying — so the state
    /// being uploaded is the one the verdict settled on, which is the only state the server will
    /// accept for a confirmed turn. A checkpoint that arrives for any other turn is one this client
    /// has moved past, and rebuilding it to upload would cost more than the checkpoint saves.
    /// </para>
    /// <para>
    /// Failure is not the session's problem. Nothing waits on a checkpoint: the next reconnect
    /// simply replays from an older one, which is what every reconnect did before they existed.
    /// </para>
    /// </remarks>
    private async Task CheckpointIfDueAsync(
        int confirmedTurn,
        string stateHash,
        CancellationToken cancellationToken)
    {
        if (!IsHost || confirmedTurn <= 0 || confirmedTurn % CheckpointEveryTurns != 0) return;
        if (_replay.State.Coordinator.Turn != confirmedTurn + 1) return;
        if (!string.Equals(
                stateHash, MatchStateHasher.ComputeSha256(_replay.State), StringComparison.Ordinal))
        {
            return;
        }
        try
        {
            await UploadSnapshotAsync(confirmedTurn, stateHash, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is MultiplayerApiException
            or MultiplayerProtocolException or RetryExhaustedException)
        {
            // A checkpoint nobody is waiting for. The next reconnect replays from an older one.
        }
    }

    private Task UploadSnapshotAsync(
        int turn,
        string stateHash,
        CancellationToken cancellationToken) =>
        CallAsync(
            token => _match.UploadSnapshotAsync(
                new UploadSnapshotRequest(
                    turn,
                    NativeSaveSerializer.CurrentFormatVersion,
                    MultiplayerProtocolVersion.Current,
                    MultiplayerSessionVersion.Current,
                    stateHash,
                    MatchStateClone.ToBase64(_replay.State),
                    SummarizeSeats(_replay.State)),
                token),
            _pumpLane,
            cancellationToken);
}
