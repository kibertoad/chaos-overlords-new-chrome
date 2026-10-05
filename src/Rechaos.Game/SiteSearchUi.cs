using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
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

    /// <summary>FND-SEARCH-001: a row's name is cut to its first 15 characters.</summary>
    public const int NameCharacters = 15;

    /// <summary>FND-SEARCH-001: row <paramref name="index"/>'s 20-by-14 marker icon.</summary>
    public static Rectangle Icon(int index) => new(Site(index).Location, new Point(20, 14));

    /// <summary>FND-SEARCH-001: the icon of site definition <paramref name="definition"/> in PX00150.</summary>
    public static Rectangle IconSource(int definition)
    {
        if (definition is < 0 or >= MaximumSites) throw new ArgumentOutOfRangeException(nameof(definition));
        return new Rectangle(definition % RowsPerColumn * 20, definition / RowsPerColumn * 14, 20, 14);
    }

    public static Rectangle Site(int index)
    {
        if (index is < 0 or >= MaximumSites) throw new ArgumentOutOfRangeException(nameof(index));
        // Native Search handler 0x00448E32 (FND-SEARCH-002) constructs these exact half-open
        // targets with Point(102 + 116 * column, 22 + 15 * row), 114, 15.
        return SharedPanelLayout.At(102 + index / RowsPerColumn * 116,
            22 + index % RowsPerColumn * 15, 114, 15);
    }
}

/// <summary>The control of the Search panel a press lands on (FND-SEARCH-002).</summary>
public enum SiteSearchControl
{
    Nothing,
    All,
    None,
    Done,
    Row
}

/// <summary>A press on the Search panel: the control it hit and, for a row, the row's index.</summary>
public readonly record struct SiteSearchPress(SiteSearchControl Control, int Row = -1);

/// <summary>A pointer press handled by the open panel, and whether it opens a row's Site Information.</summary>
public readonly record struct SiteSearchClick(SiteSearchPress Press, bool OpensDetails);

public static class SiteSearchPanel
{
    /// <summary>The site definition of each row the panel lists, in row order.</summary>
    public static short[] Rows(OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return definitions.Sites.OrderBy(site => site.Id)
            .Take(SiteSearchLayout.MaximumSites).Select(site => site.Id).ToArray();
    }

    /// <summary>
    /// FND-SEARCH-002: the control under a press, tested as the handler tests its half-open
    /// rectangles, ALL, NONE and Done first and then the rows the panel lists.
    /// </summary>
    public static SiteSearchPress HitTest(Point point, int rowCount)
    {
        if (SiteSearchLayout.All.Contains(point)) return new(SiteSearchControl.All);
        if (SiteSearchLayout.None.Contains(point)) return new(SiteSearchControl.None);
        if (SiteSearchLayout.Ok.Contains(point)) return new(SiteSearchControl.Done);
        for (var row = 0; row < Math.Min(rowCount, SiteSearchLayout.MaximumSites); row++)
            if (SiteSearchLayout.Site(row).Contains(point)) return new(SiteSearchControl.Row, row);
        return new(SiteSearchControl.Nothing);
    }

    /// <summary>
    /// RULE-SEARCH-001: ALL selects every listed site and NONE clears the player's filter; a row
    /// flips its site. A double-click on a row opens Site Information instead, which the caller
    /// decides, and Done changes no filter.
    /// </summary>
    public static void Apply(
        SiteSearchSelectionState selections,
        PlayerId player,
        SiteSearchPress press,
        IReadOnlyList<short> rowSites)
    {
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(rowSites);
        switch (press.Control)
        {
            case SiteSearchControl.All:
                selections.SelectAll(player, rowSites);
                break;
            case SiteSearchControl.None:
                selections.Clear(player);
                break;
            case SiteSearchControl.Row:
                if (press.Row < 0 || press.Row >= rowSites.Count)
                    throw new ArgumentOutOfRangeException(nameof(press), press.Row, "A row press needs a listed row.");
                selections.Toggle(player, rowSites[press.Row]);
                break;
        }
    }

    /// <summary>
    /// A press of the pointer on the open panel: finds the control under it and changes the
    /// player's filter as <see cref="Apply"/> does, except for the second press of a double-click
    /// on a row, which changes nothing and opens that row's Site Information instead
    /// (FND-SEARCH-004). The caller closes the panel on Done and opens Site Information when
    /// <see cref="SiteSearchClick.OpensDetails"/> is set.
    /// </summary>
    public static SiteSearchClick Press(
        SiteSearchSelectionState selections,
        PlayerId player,
        Point point,
        IReadOnlyList<short> rowSites,
        IndexedDoubleClickTracker clicks,
        TimeSpan time)
    {
        ArgumentNullException.ThrowIfNull(rowSites);
        ArgumentNullException.ThrowIfNull(clicks);
        var press = HitTest(point, rowSites.Count);
        // FND-SEARCH-004: the original opens Site Information on the window's double-click, which
        // Windows reports only for a second press close to the first, so a press on ALL, NONE or
        // no control between two presses of a row keeps the second a plain press.
        if (press.Control != SiteSearchControl.Row)
        {
            clicks.Cancel();
        }
        else if (clicks.Register(press.Row, time))
        {
            return new SiteSearchClick(press, OpensDetails: true);
        }
        Apply(selections, player, press, rowSites);
        return new SiteSearchClick(press, OpensDetails: false);
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
        for (var sectorIndex = 0; sectorIndex < state.Sectors.Count; sectorIndex++)
        {
            var sector = state.Sectors[sectorIndex];
            var visibleSlot = 0;
            for (var siteIndex = 0; siteIndex < sector.Sites.Count; siteIndex++)
            {
                var site = sector.Sites[siteIndex];
                var controlled = SiteControlRules.Controller(
                    sector, site, state.Definitions.Site(site.DefinitionId)) == player;
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
        // The uncontrolled icons sit in the two rows below the controlled ones Search draws.
        var icon = SiteSearchLayout.IconSource(marker.SiteDefinitionId);
        return marker.Controlled ? icon : icon with { Y = icon.Y + 28 };
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
