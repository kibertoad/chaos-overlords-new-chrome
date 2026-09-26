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
    /// The checkpoint or bootstrap upload running behind the pump, if any.
    /// </summary>
    /// <remarks>
    /// Written only by the pump, which is the only thing that starts one; awaited by the disposal.
    /// </remarks>
    private Task _backgroundUpload = Task.CompletedTask;

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
    private void UploadBootstrapSnapshotIfDue()
    {
        if (!_uploadInitialSnapshot) return;
        // See above: a bootstrap nobody is waiting on. Owed until one actually starts: a restore
        // re-arms this while the first upload may still be retrying behind the pump, and that one
        // can yet give up, so a re-arm met by a busy upload is kept for the next offer rather than
        // spent on nothing. An offer that comes after turn 1 has sealed is refused `unknown_turn`
        // and dropped like any other refusal.
        _uploadInitialSnapshot = !StartBackgroundUpload(
            0, MatchStateHasher.ComputeFingerprint(_replay.State));
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
    private void CheckpointIfDue(int confirmedTurn, string stateHash)
    {
        if (!IsHost || confirmedTurn <= 0 || confirmedTurn % CheckpointEveryTurns != 0) return;
        if (_replay.State.Coordinator.Turn != confirmedTurn + 1) return;
        if (!string.Equals(
                stateHash, MatchStateHasher.ComputeFingerprint(_replay.State), StringComparison.Ordinal))
        {
            return;
        }
        // A checkpoint nobody is waiting for. The next reconnect replays from an older one.
        _ = StartBackgroundUpload(confirmedTurn, stateHash);
    }

    /// <summary>
    /// Uploads a snapshot nothing on this client waits for, behind the pump and off the lanes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The upload used to run inside the pump, on the pump's lane and the five-minute call window.
    /// A checkpoint met by a rate limit or a server hiccup therefore put the reconnect modal in
    /// front of the host while they planned the next turn, and held every event behind it —
    /// readiness, the next seal — for as long as the window lasted, over a stream that was fine.
    /// </para>
    /// <para>
    /// The request is built here, on the pump, because the state it serialises is the pump's and
    /// moves on with the next event. Only the round trip leaves. One upload at a time: a checkpoint
    /// is due every ten turns and gives up within minutes, so one still running when the next is
    /// due is a server that is not taking them, and a second would only queue behind it.
    /// </para>
    /// </remarks>
    /// <returns>Whether the upload started; false while an earlier one is still running.</returns>
    private bool StartBackgroundUpload(int turn, string stateHash)
    {
        if (!_backgroundUpload.IsCompleted) return false;
        var request = new UploadSnapshotRequest(
            turn,
            NativeSaveSerializer.CurrentFormatVersion,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current,
            stateHash,
            MatchStateClone.ToBase64(_replay.State),
            SummarizeSeats(_replay.State));
        var cancellationToken = _stoppingToken;
        _backgroundUpload = Task.Run(
            () => TryUploadSnapshotAsync(request, cancellationToken), cancellationToken);
        return true;
    }

    /// <summary>Uploads a snapshot nothing on this client waits for, so a refusal is dropped.</summary>
    private async Task TryUploadSnapshotAsync(
        UploadSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await CallAsync(
                token => _match.UploadSnapshotAsync(request, token),
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
        catch (Exception exception)
        {
            // Not a refusal and not an outage: something this build did not expect, which the
            // pump used to end the session over when the upload ran on it. It still does.
            Fail(exception, "upload_snapshot");
        }
    }
}
