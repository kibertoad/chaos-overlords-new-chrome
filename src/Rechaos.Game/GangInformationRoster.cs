using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class GangInformationRoster
{
    public static IReadOnlyList<MatchGangState> ForSector(
        IEnumerable<MatchGangState> gangs,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return gangs
            .Where(gang => gang.IsActive && gang.SectorId == sectorId)
            .OrderBy(gang => gang.Id.Value)
            .ToArray();
    }

    /// <summary>
    /// The gangs one player has in a sector, out of those the viewer can see there, in the order
    /// <see cref="ForSector"/> gives.
    /// </summary>
    /// <remarks>
    /// What the arrow keys step through on another player's gang panel: that player's gangs, and
    /// only those the viewer already sees, so cycling can neither jump to the viewer's own gangs
    /// nor reveal a hidden one.
    /// </remarks>
    public static IReadOnlyList<MatchGangState> ForOwnerInSector(
        IEnumerable<MatchGangState> visible,
        PlayerId owner,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(visible);
        return ForSector(visible.Where(gang => gang.Owner == owner), sectorId);
    }
}
