using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// FMT-SAVE-001 <c>cursor_sectors</c>, FND-SAVE-003: each player's selected sector, kept while
/// other players plan. A new game starts every player on the sector of its roster slot 0, a
/// player's planning starts on its own kept sector, and the selection it leaves is kept when its
/// planning ends.
/// </summary>
public sealed class PlanningSelectionMemory
{
    private readonly Dictionary<PlayerId, int> _sectors = [];

    /// <summary>Starts every player on the sector of its roster slot 0, as a new game does.</summary>
    public void Reset(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _sectors.Clear();
        foreach (var player in state.Players)
            if (player.Gangs.Count > 0)
                _sectors[player.Id] = player.Gangs[0].SectorId;
    }

    public void Store(PlayerId player, int sectorId) => _sectors[player] = sectorId;

    /// <summary>
    /// FMT-SAVE-001 <c>cursor_sectors</c>: the kept sector of each player slot, -1 for a slot that
    /// keeps none, as a save records them.
    /// </summary>
    public IReadOnlyList<int> Snapshot() =>
        Enumerable.Range(0, MatchLimits.PlayerCount)
            .Select(slot => _sectors.GetValueOrDefault(new PlayerId(slot), -1))
            .ToArray();

    /// <summary>
    /// FND-SAVE-003: a loaded match restores each player's kept sector from the save. A slot the
    /// save keeps no sector for, or one outside the city, starts on the sector of its roster slot
    /// 0, as a new game does.
    /// </summary>
    public void Restore(MatchState state, IReadOnlyList<int>? sectors)
    {
        Reset(state);
        if (sectors is null) return;
        foreach (var player in state.Players)
            if (player.Id.Value >= 0 && player.Id.Value < sectors.Count
                && sectors[player.Id.Value] is var sector && sector >= 0 && sector < state.Sectors.Count)
                _sectors[player.Id] = sector;
    }

    /// <summary>The player's kept sector, or <paramref name="fallback"/> when it has none.</summary>
    public int For(PlayerId player, int fallback) => _sectors.GetValueOrDefault(player, fallback);
}
