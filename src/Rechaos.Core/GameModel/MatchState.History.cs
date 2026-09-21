namespace Rechaos.Core.GameModel;

/// <summary>The two append-only histories a match carries: its events and its phase-boundary fingerprints.</summary>
public sealed partial class MatchState
{
    private readonly List<GameEvent> _events = [];
    private readonly IReadOnlyList<GameEvent> _eventView;
    private readonly List<PhaseBoundaryHash> _phaseHashes = [];
    private readonly IReadOnlyList<PhaseBoundaryHash> _phaseHashView;
    /// <summary>Scratch space for encoding one history entry before it is chained.</summary>
    private readonly MemoryStream _entryBytes = new();
    private UInt128 _eventHistoryDigest;
    private UInt128 _phaseHashHistoryDigest;
    private long _nextEventSequence;

    public IReadOnlyList<GameEvent> Events => _eventView;
    public IReadOnlyList<PhaseBoundaryHash> PhaseHashes => _phaseHashView;
    internal long NextEventSequence => _nextEventSequence;

    /// <summary>
    /// A digest of the whole event history, chained entry by entry as each event was stored.
    /// </summary>
    /// <remarks>
    /// The fingerprint folds this in rather than re-encoding every event: the history only grows,
    /// a match fingerprints itself thousands of times, and hashing the whole history each time
    /// made a fingerprint cost more with every turn played. Restoring a match stores its events
    /// through the same path, so a restored digest is the digest the match had when it was saved.
    /// </remarks>
    internal UInt128 EventHistoryDigest => _eventHistoryDigest;

    /// <summary>The phase-boundary history's digest; see <see cref="EventHistoryDigest"/>.</summary>
    internal UInt128 PhaseHashHistoryDigest => _phaseHashHistoryDigest;

    private GameEvent StoreEvent(GameEvent gameEvent)
    {
        var frozen = CanonicalEventWriter.Freeze(gameEvent);
        _events.Add(frozen);
        _entryBytes.SetLength(0);
        CanonicalEventWriter.Append(_entryBytes, frozen);
        _eventHistoryDigest = MatchStateHasher.Chain(_eventHistoryDigest, EncodedEntry());
        return frozen;
    }

    private TurnTransition CaptureBoundary(TurnTransition transition)
    {
        StorePhaseHash(new PhaseBoundaryHash(
            Coordinator.Turn,
            Coordinator.Phase,
            Coordinator.ExecutionPhase,
            MatchStateHasher.ComputePhaseBoundaryFingerprint(this)));
        return transition;
    }

    private void StorePhaseHash(PhaseBoundaryHash boundary)
    {
        _phaseHashes.Add(boundary);
        _entryBytes.SetLength(0);
        PhaseBoundaryHash.WriteCanonical(_entryBytes, boundary);
        _phaseHashHistoryDigest = MatchStateHasher.Chain(_phaseHashHistoryDigest, EncodedEntry());
    }

    private ReadOnlySpan<byte> EncodedEntry() =>
        _entryBytes.GetBuffer().AsSpan(0, checked((int)_entryBytes.Length));
}
