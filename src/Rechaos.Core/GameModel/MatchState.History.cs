namespace Rechaos.Core.GameModel;

/// <summary>The two append-only histories a match carries: its events and its phase-boundary fingerprints.</summary>
public sealed partial class MatchState
{
    private readonly List<GameEvent> _events = [];
    private readonly IReadOnlyList<GameEvent> _eventView;
    private readonly MemoryStream _canonicalEventBytes = new();
    private readonly List<PhaseBoundaryHash> _phaseHashes = [];
    private readonly IReadOnlyList<PhaseBoundaryHash> _phaseHashView;
    private readonly MemoryStream _canonicalPhaseHashBytes = new();
    private long _nextEventSequence;

    public IReadOnlyList<GameEvent> Events => _eventView;
    public IReadOnlyList<PhaseBoundaryHash> PhaseHashes => _phaseHashView;
    internal long NextEventSequence => _nextEventSequence;

    /// <summary>
    /// The event history in its canonical encoding, appended as each event was stored.
    /// </summary>
    /// <remarks>
    /// The hasher reads this instead of re-encoding every event: the history only grows, so the
    /// bytes it produced last time are still the bytes it produces now, and a match hashes its
    /// state thousands of times.
    /// </remarks>
    internal ReadOnlySpan<byte> CanonicalEventHistory =>
        _canonicalEventBytes.GetBuffer().AsSpan(0, checked((int)_canonicalEventBytes.Length));

    /// <summary>The phase-boundary history in its canonical encoding; see <see cref="CanonicalEventHistory"/>.</summary>
    internal ReadOnlySpan<byte> CanonicalPhaseHashHistory =>
        _canonicalPhaseHashBytes.GetBuffer().AsSpan(0, checked((int)_canonicalPhaseHashBytes.Length));

    private GameEvent StoreEvent(GameEvent gameEvent)
    {
        var frozen = CanonicalEventWriter.Freeze(gameEvent);
        _events.Add(frozen);
        CanonicalEventWriter.Append(_canonicalEventBytes, frozen);
        return frozen;
    }

    private TurnTransition CaptureBoundary(TurnTransition transition)
    {
        StorePhaseHash(new PhaseBoundaryHash(
            Coordinator.Turn,
            Coordinator.Phase,
            Coordinator.ExecutionPhase,
            MatchStateHasher.ComputeVersionTwentyThreeSha256(this)));
        return transition;
    }

    private void StorePhaseHash(PhaseBoundaryHash boundary)
    {
        _phaseHashes.Add(boundary);
        PhaseBoundaryHash.AppendCanonical(_canonicalPhaseHashBytes, boundary);
    }
}
