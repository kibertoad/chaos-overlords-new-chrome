using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

/// <summary>
/// The rolling autosave's write queue: one worker at a time, the newest turn coalesced behind it.
/// </summary>
/// <remarks>
/// The match mutates on the game thread, so a turn is captured there and only the bytes reach the
/// worker, which writes and validates them. The game thread owns this object: every member must be
/// called from it, and <see cref="Flush"/> must run before that thread touches the file itself,
/// because the worker promotes its generation by replacing the primary.
/// </remarks>
internal sealed class RollingAutoSave
{
    /// <summary>Durably writes one captured generation over the current one.</summary>
    /// <param name="row">
    /// The browser row for this generation, captured on the game thread with the bytes.
    /// </param>
    /// <param name="trustExistingPrimary">
    /// Whether the writer may keep the existing primary as the next backup generation without
    /// reading it back first, because the caller knows this process wrote and verified it.
    /// </param>
    internal delegate void Writer(
        ReadOnlyMemory<byte> snapshot, OriginalData definitions, SaveSlotSummary row,
        bool trustExistingPrimary);

    private readonly Writer _write;
    private readonly Action<int, Exception> _reportFailure;
    private Task? _task;
    private Snapshot? _pending;
    private int _activeTurn;

    /// <summary>
    /// Whether the file on disk is a generation this process wrote and read back, so the next
    /// write may promote it to the backup without deserializing it again.
    /// </summary>
    /// <remarks>
    /// This has to be forgotten as readily as it is learned. A failed write leaves the primary in
    /// an unknown state, and loading the autosave either replaces the primary from the backup or
    /// leaves a damaged one in place. Trusting the primary after any of those would move a damaged
    /// file over the known-good backup, which is the one thing the atomic writer promises never to
    /// do; proving it again costs one deserialization, and only when something has gone wrong.
    /// </remarks>
    private bool _primaryVerified;

    public RollingAutoSave(string path, Action<int, Exception> reportFailure)
        : this(
            (snapshot, definitions, row, trustExistingPrimary) =>
            {
                NativeSaveStore.SaveAtomic(path, snapshot, definitions, trustExistingPrimary);
                // The sidecar describes the file that has just been promoted, so it is written
                // here rather than on the game thread: what it records for the staleness check is
                // that file's own length and write time, and only this thread knows when the
                // promotion happened. It never throws, and never stands between a durable
                // generation and the next one.
                SaveSlotCatalog.WriteAutoSaveMetadata(path, row, definitions);
            },
            reportFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
    }

    internal RollingAutoSave(Writer write, Action<int, Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(reportFailure);
        _write = write;
        _reportFailure = reportFailure;
    }

    /// <summary>Captures the state on this thread and hands the bytes to the worker.</summary>
    public void Capture(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var turn = state.Coordinator.Turn;
        Snapshot snapshot;
        try
        {
            snapshot = new Snapshot(
                NativeSaveStore.Serialize(state), state.Definitions,
                SaveSlotCatalog.DescribeAutoSave(state), turn);
        }
        catch (Exception exception) when (IsSaveFailure(exception))
        {
            _reportFailure(turn, exception);
            return;
        }

        ObserveCompleted(wait: false);
        if (_task is not null)
        {
            // Only the newest capture is worth keeping: an older one the worker has not reached
            // yet would be overwritten by this turn the moment it landed.
            _pending = snapshot;
            return;
        }

        Start(snapshot);
    }

    /// <summary>Observes finished writes and starts the newest pending capture, if any.</summary>
    public void Pump() => ObserveCompleted(wait: false);

    /// <summary>
    /// Blocks until nothing is being written, so this thread may read or replace the file.
    /// </summary>
    /// <remarks>
    /// The worker promotes its generation by renaming over the primary, which fails on Windows
    /// while a reader holds that file open, and a read that slipped in ahead of the promotion
    /// would see the previous turn. Draining the worker first avoids both. This happens only when
    /// the player opens the save browser, loads the autosave, or leaves the game, never in the
    /// turn loop.
    /// </remarks>
    public void Flush() => ObserveCompleted(wait: true);

    /// <summary>
    /// Forgets that this process verified the file, after a load replaced it or left it damaged.
    /// </summary>
    public void ForgetVerifiedPrimary() => _primaryVerified = false;

    private void ObserveCompleted(bool wait)
    {
        while (_task is not null && (wait || _task.IsCompleted))
        {
            var task = _task;
            var completedTurn = _activeTurn;
            _task = null;
            try
            {
                task.GetAwaiter().GetResult();
                _primaryVerified = true;
            }
            catch (Exception exception) when (IsSaveFailure(exception))
            {
                // A failed write can have stopped anywhere, including partway through promoting
                // its generation, so what is on disk is no longer known to be good.
                _primaryVerified = false;
                _reportFailure(completedTurn, exception);
            }

            if (_pending is not { } pending) return;
            _pending = null;
            Start(pending);
        }
    }

    private void Start(Snapshot snapshot)
    {
        _activeTurn = snapshot.Turn;
        var trustExistingPrimary = _primaryVerified;
        _task = Task.Run(() => _write(
            snapshot.Bytes, snapshot.Definitions, snapshot.Row, trustExistingPrimary));
    }

    private static bool IsSaveFailure(Exception exception) =>
        exception is IOException or InvalidDataException or UnauthorizedAccessException;

    /// <param name="Row">
    /// What the save browser draws for this generation. Taken on the game thread with the bytes:
    /// the match it describes keeps moving, and the worker may only see the turn it was handed.
    /// </param>
    private sealed record Snapshot(
        byte[] Bytes, OriginalData Definitions, SaveSlotSummary Row, int Turn);
}
