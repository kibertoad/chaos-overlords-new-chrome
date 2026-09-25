using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class GangInformationRoster
{
    /// <summary>
    /// RULE-UI-010 <c>sector_roster_slots</c>: the player's active gangs in the sector, in roster
    /// slot order, whatever their visibility.
    /// </summary>
    public static IReadOnlyList<MatchGangState> ForSector(
        IEnumerable<MatchGangState> gangs,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return gangs
            .Where(gang => gang.IsActive && gang.SectorId == sectorId)
            .ToArray();
    }
}
