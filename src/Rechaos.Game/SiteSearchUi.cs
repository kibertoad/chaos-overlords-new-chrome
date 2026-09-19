using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class SiteSearchLayout
{
    public const int ColumnCount = 2;
    public const int RowsPerColumn = 11;
    public const int MaximumSites = ColumnCount * RowsPerColumn;

    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle All => SharedPanelLayout.At(33, 16, 49, 23);
    public static Rectangle None => SharedPanelLayout.At(33, 48, 49, 23);
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);

    public static Rectangle Site(int index)
    {
        if (index is < 0 or >= MaximumSites) throw new ArgumentOutOfRangeException(nameof(index));
        // Native Search handler 0x00448e32 constructs these exact half-open
        // targets with Point(102 + 116 * column, 22 + 15 * row), 114, 15.
        return SharedPanelLayout.At(102 + index / RowsPerColumn * 116,
            22 + index % RowsPerColumn * 15, 114, 15);
    }
}

public sealed class SiteSearchSelectionState
{
    private readonly HashSet<short>[] _selections = Enumerable.Range(0, MatchLimits.PlayerCount)
        .Select(_ => new HashSet<short>()).ToArray();

    public bool IsSelected(PlayerId player, short siteId)
    {
        Validate(player, siteId);
        return _selections[player.Value].Contains(siteId);
    }

    public IReadOnlySet<short> For(PlayerId player)
    {
        ValidatePlayer(player);
        return _selections[player.Value].ToHashSet();
    }

    public void Toggle(PlayerId player, short siteId)
    {
        Validate(player, siteId);
        if (!_selections[player.Value].Remove(siteId))
            _selections[player.Value].Add(siteId);
    }

    public void SelectAll(PlayerId player, IEnumerable<short> siteIds)
    {
        ArgumentNullException.ThrowIfNull(siteIds);
        ValidatePlayer(player);
        var selected = siteIds.ToArray();
        if (selected.Any(siteId => siteId is < 0 or >= SiteSearchLayout.MaximumSites))
            throw new ArgumentOutOfRangeException(nameof(siteIds));
        _selections[player.Value].Clear();
        _selections[player.Value].UnionWith(selected);
    }

    public void Clear(PlayerId player)
    {
        ValidatePlayer(player);
        _selections[player.Value].Clear();
    }

    public void Reset()
    {
        foreach (var selection in _selections) selection.Clear();
    }

    private static void Validate(PlayerId player, short siteId)
    {
        ValidatePlayer(player);
        if (siteId is < 0 or >= SiteSearchLayout.MaximumSites)
            throw new ArgumentOutOfRangeException(nameof(siteId));
    }

    private static void ValidatePlayer(PlayerId player)
    {
        if (player.Value is < 0 or >= MatchLimits.PlayerCount)
            throw new ArgumentOutOfRangeException(nameof(player));
    }
}

public sealed record CitySiteMarker(
    int SectorId,
    short SiteDefinitionId,
    int VisibleSlot,
    bool Controlled);

public static class CitySiteMarkerProjection
{
    public static IReadOnlyList<CitySiteMarker> Project(
        MatchState state,
        PlayerId player,
        IReadOnlySet<short> selectedSiteIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selectedSiteIds);
        if (state.FindPlayer(player) is null) throw new ArgumentOutOfRangeException(nameof(player));
        var result = new List<CitySiteMarker>();
        foreach (var sector in state.Sectors.OrderBy(sector => sector.Id))
        {
            var visibleSlot = 0;
            foreach (var site in sector.Sites.OrderBy(site => site.Slot))
            {
                var controlled = SiteControlRules.Controller(sector, site) == player;
                if (!controlled && !selectedSiteIds.Contains(site.DefinitionId)) continue;
                result.Add(new CitySiteMarker(
                    sector.Id, site.DefinitionId, visibleSlot++, controlled));
            }
        }
        return result;
    }

    public static Rectangle Source(CitySiteMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        if (marker.SiteDefinitionId is < 0 or >= SiteSearchLayout.MaximumSites)
            throw new ArgumentOutOfRangeException(nameof(marker));
        return new Rectangle(
            marker.SiteDefinitionId % SiteSearchLayout.RowsPerColumn * 20,
            marker.SiteDefinitionId / SiteSearchLayout.RowsPerColumn * 14
                + (marker.Controlled ? 0 : 28),
            20,
            14);
    }

    public static Rectangle Destination(CitySiteMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        _ = CityMapLayout.Source(marker.SectorId);
        if (marker.VisibleSlot is < 0 or >= MatchLimits.SitesPerSector)
            throw new ArgumentOutOfRangeException(nameof(marker));
        return new Rectangle(
            CityMapLayout.Left + marker.SectorId % MatchLimits.BoardWidth * CityMapLayout.ColumnStride + 9,
            CityMapLayout.Top + marker.SectorId / MatchLimits.BoardWidth * CityMapLayout.RowStride
                + marker.VisibleSlot * 15 + 7,
            20,
            14);
    }
}
