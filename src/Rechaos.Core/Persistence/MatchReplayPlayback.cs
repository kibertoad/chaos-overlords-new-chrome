using System.IO.Compression;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Core.Persistence;

/// <summary>A read-only cursor over a journal whose every step was verified when it was opened.</summary>
/// <remarks>
/// <para>
/// Opening replays the whole journal once and checks each step's fingerprint, so a seek can never
/// show a state the journal does not vouch for. On that pass the cursor keeps compressed native
/// snapshots at evenly spaced positions, and a seek starts from the nearest one at or before its
/// target instead of from the opening state: a rewind of one step in a long match costs at most one
/// interval of steps. At most <see cref="MaximumCheckpoints"/> are kept, because a late-game
/// snapshot carries the whole event history and runs to megabytes: a two-year six-player match
/// keeps about 12 MiB of them. A state past the native save size limit gets no snapshot, and a seek
/// beyond it walks on from the last one written.
/// </para>
/// <para>
/// Every step a seek applies is checked against its recorded fingerprint again. A checkpoint is the
/// native save format's round trip, the same copy online play plans on, and the check is what
/// would catch it if that round trip ever stopped reproducing a state.
/// </para>
/// <para>
/// <see cref="State"/> is handed to a viewer that draws it. A move first confirms the state still
/// has the fingerprint of its position, and rebuilds it from a checkpoint when something changed
/// it, so a viewer that touched the frame never makes an intact journal report a divergence.
/// </para>
/// </remarks>
public sealed class MatchReplayPlayback
{
    /// <summary>The most snapshots a cursor keeps besides the journal's opening one.</summary>
    public const int MaximumCheckpoints = 64;

    /// <summary>The fewest steps between two snapshots, so a short journal keeps few of them.</summary>
    public const int MinimumCheckpointInterval = 32;

    private readonly ReplayDocument _document;
    private readonly OriginalData _definitions;
    private readonly int _checkpointInterval;

    /// <summary>
    /// The compressed snapshot at position <c>(k + 1) * _checkpointInterval</c>, for as many
    /// intervals as a snapshot could be written.
    /// </summary>
    private readonly List<byte[]> _checkpoints = [];

    /// <summary>The turn number at each position, from 0 through <see cref="StepCount"/>.</summary>
    private readonly int[] _turns;

    internal MatchReplayPlayback(ReplayDocument document, OriginalData definitions)
    {
        MatchReplaySerializer.ValidateDocument(document);
        _document = document;
        _definitions = definitions;
        _checkpointInterval = Math.Max(
            MinimumCheckpointInterval,
            (document.Steps.Count + MaximumCheckpoints - 1) / MaximumCheckpoints);
        _turns = new int[document.Steps.Count + 1];

        var state = MatchReplaySerializer.LoadOpeningState(document, definitions);
        _turns[0] = state.Coordinator.Turn;
        // The event history only grows, so once a state is too large for a native snapshot every
        // later one is too, and seeks past that point walk on from the last checkpoint written.
        var snapshotsFit = true;
        for (var index = 0; index < document.Steps.Count; index++)
        {
            MatchReplaySerializer.ApplyVerified(state, document.Steps[index], index);
            var position = index + 1;
            _turns[position] = state.Coordinator.Turn;
            if (snapshotsFit && position % _checkpointInterval == 0 && position < document.Steps.Count)
            {
                if (Compress(state) is { } checkpoint) _checkpoints.Add(checkpoint);
                else snapshotsFit = false;
            }
        }

        State = MatchReplaySerializer.LoadOpeningState(document, definitions);
    }

    /// <summary>The state at <see cref="Position"/>. Position zero is the opening snapshot.</summary>
    /// <remarks>A move may replace this instance; read it again after every move.</remarks>
    public MatchState State { get; private set; }

    /// <summary>How many recorded steps have been applied to reach <see cref="State"/>.</summary>
    public int Position { get; private set; }

    public int StepCount => _document.Steps.Count;

    /// <summary>The step that produced <see cref="State"/>, or null at the opening snapshot.</summary>
    public ReplayStep? CurrentStep => Position == 0 ? null : _document.Steps[Position - 1];

    /// <summary>The match turn at <paramref name="position"/>.</summary>
    public int TurnAt(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, StepCount);
        return _turns[position];
    }

    /// <summary>
    /// The first position of the current turn, or of the turn before it when the cursor already
    /// stands at a turn's first position.
    /// </summary>
    public int PreviousTurnStart()
    {
        var start = TurnStart(Position);
        return start < Position || start == 0 ? start : TurnStart(start - 1);
    }

    /// <summary>The first position of the next turn, or the end when this is the last turn.</summary>
    public int NextTurnStart()
    {
        var position = Position;
        while (position < StepCount && _turns[position] == _turns[Position]) position++;
        return position;
    }

    /// <summary>Advances one recorded step; returns false at the end.</summary>
    /// <exception cref="InvalidDataException">The step did not reproduce.</exception>
    public bool MoveNext()
    {
        if (Position == StepCount) return false;
        Seek(Position + 1);
        return true;
    }

    /// <summary>Moves to any position from 0 through <see cref="StepCount"/>.</summary>
    /// <exception cref="InvalidDataException">A step on the way did not reproduce.</exception>
    public void Seek(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, StepCount);
        var checkpoint = Math.Min(position / _checkpointInterval, _checkpoints.Count);
        var checkpointPosition = checkpoint * _checkpointInterval;
        // Walk on from the current state only when it is still the one its position vouches for
        // and no checkpoint lies between it and the target; otherwise start from the checkpoint.
        if (position < Position || checkpointPosition > Position || !StateIsIntact())
        {
            State = checkpoint == 0
                ? MatchReplaySerializer.LoadOpeningState(_document, _definitions)
                : Decompress(_checkpoints[checkpoint - 1], checkpointPosition);
            Position = checkpointPosition;
        }
        while (Position < position)
        {
            MatchReplaySerializer.ApplyVerified(State, _document.Steps[Position], Position);
            Position++;
        }
    }

    private int TurnStart(int position)
    {
        while (position > 0 && _turns[position - 1] == _turns[position]) position--;
        return position;
    }

    private string FingerprintAt(int position) => position == 0
        ? _document.InitialStateFingerprint
        : _document.Steps[position - 1].ResultingStateFingerprint;

    private bool StateIsIntact() => string.Equals(
        FingerprintAt(Position), MatchStateHasher.ComputeFingerprint(State), StringComparison.Ordinal);

    /// <summary>The state as a compressed native snapshot, or null when it exceeds the save limit.</summary>
    private static byte[]? Compress(MatchState state)
    {
        using var compressed = new MemoryStream();
        try
        {
            using var brotli = new BrotliStream(compressed, CompressionLevel.Fastest, leaveOpen: true);
            NativeSaveSerializer.Save(brotli, state);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        return compressed.ToArray();
    }

    private MatchState Decompress(byte[] checkpoint, int position)
    {
        using var compressed = new MemoryStream(checkpoint, writable: false);
        using var brotli = new BrotliStream(compressed, CompressionMode.Decompress);
        var state = NativeSaveSerializer.Load(brotli, _definitions);
        MatchReplaySerializer.VerifyFingerprint(FingerprintAt(position), state, position - 1);
        return state;
    }
}

/// <summary>A playback cursor and, when the backup generation was opened instead, why.</summary>
/// <param name="PrimaryFailure">
/// Why the primary journal could not be played, or null when the cursor plays the primary.
/// </param>
public sealed record MatchReplayPlaybackLoadResult(
    MatchReplayPlayback Playback,
    ReplayFailure? PrimaryFailure)
{
    public bool OpenedBackup => PrimaryFailure is not null;
}
