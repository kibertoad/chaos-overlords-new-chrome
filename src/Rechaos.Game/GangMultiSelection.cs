using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The gangs a player has ctrl-picked on the Sector workspace's cards, so one order can be put to
/// all of them at once.
/// </summary>
/// <remarks>
/// The picks belong to the sector they were made in: the workspace only ever shows one sector's
/// cards, and a selection carried into another one would order gangs the player can no longer see.
/// Insertion order is the player's own priority order, and it decides who is served first when an
/// order can only be carried out in part.
/// </remarks>
public sealed class GangMultiSelection
{
    private const int NoSector = -1;
    private readonly List<GangId> _gangs = [];
    private int _sector = NoSector;

    public int Count => _gangs.Count;

    /// <summary>The picked gangs, in the order they were picked.</summary>
    public IReadOnlyList<GangId> Gangs => _gangs;

    /// <summary>Whether there is more than one gang to spread an order over.</summary>
    public bool IsBulk => _gangs.Count > 1;

    public bool Contains(GangId gang) => _gangs.Contains(gang);

    /// <summary>Picks a gang, or drops it again when a second ctrl-click takes it back out.</summary>
    public void Toggle(GangId gang, int sector)
    {
        if (sector is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sector));
        if (_sector != sector)
        {
            _gangs.Clear();
            _sector = sector;
        }
        if (!_gangs.Remove(gang)) _gangs.Add(gang);
        if (_gangs.Count == 0) _sector = NoSector;
    }

    public void Clear()
    {
        _gangs.Clear();
        _sector = NoSector;
    }

    /// <summary>Forgets a selection made in a sector other than the one now on show.</summary>
    public void KeepOnly(int sector)
    {
        if (_sector != sector) Clear();
    }

    /// <summary>The status console's account of how many gangs are picked.</summary>
    public static string Status(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return count == 1 ? "1 GANG SELECTED" : $"{count} GANGS SELECTED";
    }
}
