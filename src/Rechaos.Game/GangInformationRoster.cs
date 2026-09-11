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
}
