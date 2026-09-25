using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The <c>gangs_seen</c> bytes RULE-UI-006 reads, from one player's view, as they stood when the
/// snapshot was taken: which sectors hold a gang of the player's own and which hold an enemy gang
/// the player could see.
/// </summary>
/// <remarks>
/// The original sets these bytes from each gang's <c>visible_to</c>, which RULE-DETECT-001
/// rebuilds when planning starts (FND-UI-018). Equipment bought or given during planning changes
/// a gang's Detect at once, but no marker changes until the next planning entry takes a new
/// snapshot.
/// </remarks>
public sealed class GangSightSnapshot
{
    private readonly bool[] _present;
    private readonly bool[] _enemySeen;

    private GangSightSnapshot(bool[] present, bool[] enemySeen)
    {
        _present = present;
        _enemySeen = enemySeen;
    }

    public bool PlayerPresent(int sector) => _present[sector];

    public bool EnemySeen(int sector) => _enemySeen[sector];

    public static GangSightSnapshot Capture(MatchState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        var owner = state.FindPlayer(player) ?? throw new ArgumentOutOfRangeException(nameof(player));
        var present = new bool[MatchLimits.SectorCount];
        var enemySeen = new bool[MatchLimits.SectorCount];
        foreach (var gang in owner.Gangs.Where(gang => gang.IsActive))
            present[gang.SectorId] = true;
        foreach (var other in state.Players.Where(other => other.Id != player))
        foreach (var gang in other.Gangs.Where(gang => gang.IsActive))
            if (!enemySeen[gang.SectorId] && state.CanPlayerDetectGang(player, gang.Id))
                enemySeen[gang.SectorId] = true;
        return new GangSightSnapshot(present, enemySeen);
    }
}

/// <summary>
/// Keeps one <see cref="GangSightSnapshot"/> per planning entry (RULE-UI-006).
/// </summary>
/// <remarks>
/// The snapshot is taken the first time a marker is drawn in a planning phase, which comes before
/// the player can give any order in it, and is kept until the turn, the planning player or the
/// match changes. Outside planning the match is resolving and the markers are read from it as it
/// stands.
/// </remarks>
public sealed class GangSightSnapshotCache
{
    private MatchState? _state;
    private int _turn;
    private PlayerId _player;
    private GangSightSnapshot? _snapshot;

    public GangSightSnapshot For(MatchState state, PlayerId player)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Coordinator.Phase != TurnPhase.Command)
        {
            Clear();
            return GangSightSnapshot.Capture(state, player);
        }
        if (_snapshot is null || !ReferenceEquals(_state, state)
            || _turn != state.Coordinator.Turn || _player != player)
        {
            _snapshot = GangSightSnapshot.Capture(state, player);
            _state = state;
            _turn = state.Coordinator.Turn;
            _player = player;
        }
        return _snapshot;
    }

    public void Clear()
    {
        _snapshot = null;
        _state = null;
    }
}
