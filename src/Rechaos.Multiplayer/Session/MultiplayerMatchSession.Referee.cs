using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// A match whose server resolves every turn itself (docs/MULTIPLAYER.md, "Resolving turns on the
/// server"): the server's state decides each turn, so a client whose state differs is off on its
/// own and adopts the server's snapshot of the turn. Nobody else is paused or asked anything.
/// </summary>
public sealed partial class MultiplayerMatchSession
{
    /// <summary>Whether the server referees this match, as the match view last said.</summary>
    private bool _refereed;

    /// <summary>
    /// The first turn whose confirmation, met while catching up on the history, differs from the
    /// state this client rebuilt, or null. Only a refereed match carries one: anywhere else a
    /// confirmation is what every client reported, and a client that cannot reach it is playing
    /// different rules.
    /// </summary>
    private int? _divergedTurn;

    /// <summary>
    /// The server told a seat that its report of a turn differs from the server's state. If the
    /// seat is this one and its state after that turn is still not the server's, adopt the server's
    /// snapshot of the turn and catch back up.
    /// </summary>
    /// <remarks>
    /// Delivery is at least once and the log is replayed on every reconnect, so the announcement
    /// of a divergence long since adopted arrives here routinely; the state after the turn is what
    /// says whether anything is left to do, as for a repair.
    /// </remarks>
    private async Task AdoptServerStateAsync(
        TurnDivergedEventPayload diverged,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(diverged.PlayerId, PlayerId, StringComparison.Ordinal)) return;
        // A turn this client has not resolved yet is not one it can be off on.
        if (diverged.Turn >= Replay.State.Coordinator.Turn) return;
        if (await HoldsStateAfterTurnAsync(diverged.Turn, diverged.StateHash, cancellationToken)
            .ConfigureAwait(false))
        {
            return;
        }
        var snapshot = await CallAsync(
            token => _match.SnapshotAsync(diverged.Turn, token), _pumpLane, cancellationToken)
            .ConfigureAwait(false);
        RequireServerSnapshot(snapshot, diverged.Turn, diverged.StateHash);
        await AdoptRepairAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// After a restore's walk of the history, puts this client on the server's state from the
    /// first turn it diverged on, when the server has stored its snapshot of that turn.
    /// </summary>
    /// <remarks>
    /// The server stores the snapshot when this client's differing report reaches it. A client that
    /// stopped before reporting finds none: its report goes out with the others the restore sends,
    /// and the server answers it with <c>turn.diverged</c> on the live stream. The reports owed for
    /// turns after the adopted one are taken again from the adopted state.
    /// </remarks>
    private async Task AdoptDivergedTurnAsync(CancellationToken cancellationToken)
    {
        if (_divergedTurn is not { } turn) return;
        _divergedTurn = null;
        var snapshot = await SnapshotForTurnOrNullAsync(turn, cancellationToken).ConfigureAwait(false);
        if (snapshot is null) return;
        if (snapshot.Turn != turn)
        {
            throw new MultiplayerProtocolException(
                $"the server answered turn {turn}'s snapshot with the snapshot for turn {snapshot.Turn}");
        }
        var liveTurn = Replay.State.Coordinator.Turn;
        var rebuilt = await RebuildAsync(
                snapshot,
                throughTurn: liveTurn - 1,
                handoversThroughTurn: liveTurn,
                captureReports: true,
                cancellationToken)
            .ConfigureAwait(false);
        _history.Adopt(rebuilt.Recorder);
        _canonicalThroughTurn = snapshot.Turn;
        var owed = _unreportedSeals.Select(seal => seal.Turn).ToHashSet();
        _unreportedSeals.RemoveAll(seal => seal.Turn >= snapshot.Turn);
        _unreportedSeals.AddRange(
            rebuilt.Reports.Where(report => report.Turn > snapshot.Turn && owed.Contains(report.Turn)));
    }

    private static void RequireServerSnapshot(SnapshotView snapshot, int turn, string stateHash)
    {
        if (snapshot.Turn != turn)
        {
            throw new MultiplayerProtocolException(
                $"the server answered turn {turn}'s snapshot with the snapshot for turn {snapshot.Turn}");
        }
        if (!string.Equals(snapshot.StateHash, stateHash, StringComparison.Ordinal))
        {
            throw new MultiplayerProtocolException(
                $"the server's snapshot of turn {turn} is not the state it said the turn reached");
        }
    }
}
