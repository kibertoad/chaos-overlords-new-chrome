using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class SiteSearchLayout
{
    public const int ColumnCount = 2;
    public const int RowsPerColumn = 11;
    public const int MaximumSites = ColumnCount * RowsPerColumn;

    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle All => new(137, 141, 49, 22);
    public static Rectangle None => new(137, 174, 49, 22);
    public static Rectangle Ok => new(137, 294, 49, 22);

    public static Rectangle Site(int index)
    {
        if (index is < 0 or >= MaximumSites) throw new ArgumentOutOfRangeException(nameof(index));
        return new Rectangle(200 + index / RowsPerColumn * 119,
            143 + index % RowsPerColumn * 16, 115, 14);
    }
}

public static class SiteSearchProjection
{
    public static IReadOnlyList<int> MatchingSectors(MatchState state, IReadOnlySet<short> siteIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(siteIds);
        return state.Sectors
            .Where(sector => sector.Sites.Any(site => siteIds.Contains(site.DefinitionId)))
            .Select(sector => sector.Id)
            .ToArray();
    }
}
