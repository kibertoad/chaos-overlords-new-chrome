using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// SCR-UI-004, FND-UI-018: the detailed sector screen. The nine-sector display is the
/// 162-by-156 area of the prepared city map whose corner is the top-left neighbour's cell,
/// copied to <c>(64,60)</c> under a keyed frame.
/// </summary>
public static partial class SectorDetailLayout
{
    public const int Columns = 3;
    public const int Rows = 3;
    public static Rectangle Display => new(64, 60, 162, 156);
    public static Rectangle DisplayFrameSource => new(0, 15, 162, 156);
    public static Rectangle Back => new(4, 394, 32, 63);
    public static Rectangle OwnerStrip => new(4, 43, 32, 207);
    public static Rectangle BackStrip => new(4, 250, 32, 207);
    public static Rectangle BackStripSource => new(460, 67, 32, 207);
    public static Rectangle Workspace => new(36, 42, 398, 416);

    /// <summary>The strip for the sector's owner, the first one for no owner.</summary>
    public static Rectangle OwnerStripSource(PlayerId? owner) =>
        new(236 + 32 * (owner?.Value + 1 ?? 0), 67, 32, 207);

    /// <summary>The part of the city map the display shows, in map coordinates.</summary>
    public static Rectangle DisplaySource(int centerSectorId)
    {
        _ = CityMapLayout.Source(centerSectorId);
        return new Rectangle(
            CityMapLayout.GridInsetX + (centerSectorId % 8 - 1) * CityMapLayout.ColumnStride,
            CityMapLayout.GridInsetY + (centerSectorId / 8 - 1) * CityMapLayout.RowStride,
            Display.Width, Display.Height);
    }

    /// <summary>The whole 54-by-52 cell at column and row of the display.</summary>
    public static Rectangle Cell(int column, int row)
    {
        if (column is < 0 or >= Columns) throw new ArgumentOutOfRangeException(nameof(column));
        if (row is < 0 or >= Rows) throw new ArgumentOutOfRangeException(nameof(row));
        return new Rectangle(
            Display.X + column * CityMapLayout.ColumnStride,
            Display.Y + row * CityMapLayout.RowStride,
            CityMapLayout.TileWidth,
            CityMapLayout.TileHeight);
    }

    /// <summary>FND-UI-018: the 52-by-50 inside of a cell that <c>fn_0041A0D4</c> lightens.</summary>
    public static Rectangle CellInterior(int column, int row)
    {
        var cell = Cell(column, row);
        return new Rectangle(cell.X + 1, cell.Y + 1, cell.Width - 2, cell.Height - 2);
    }

    /// <summary>
    /// FND-UI-018: the black <c>fn_00411119</c> lays over the row or column of the display that
    /// lies off the map.
    /// </summary>
    public static IEnumerable<Rectangle> OffMapBands(int centerSectorId)
    {
        _ = CityMapLayout.Source(centerSectorId);
        var column = centerSectorId % 8;
        var row = centerSectorId / 8;
        var display = Display;
        if (row == 0) yield return new Rectangle(display.X, display.Y, display.Width, 51);
        if (row == 7) yield return new Rectangle(display.X, display.Y + 103, display.Width, display.Height - 103);
        if (column == 0) yield return new Rectangle(display.X, display.Y, 53, display.Height);
        if (column == 7) yield return new Rectangle(display.X + 107, display.Y, display.Width - 107, display.Height);
    }

    public static int? SectorAt(int centerSectorId, int column, int row)
    {
        _ = CityMapLayout.Source(centerSectorId);
        _ = Cell(column, row);
        var sectorColumn = centerSectorId % 8 + column - 1;
        var sectorRow = centerSectorId / 8 + row - 1;
        return sectorColumn is < 0 or >= 8 || sectorRow is < 0 or >= 8
            ? null
            : sectorRow * 8 + sectorColumn;
    }

    /// <summary>
    /// FND-UI-015: a point of the display falls in column <c>(x - 64 &gt; 53) + (x - 64 &gt; 107)</c>
    /// and row <c>(y - 60 &gt; 51) + (y - 60 &gt; 103)</c>.
    /// </summary>
    public static bool TryCellAt(Point point, out int column, out int row)
    {
        if (!Display.Contains(point))
        {
            column = row = -1;
            return false;
        }
        var x = point.X - Display.X;
        var y = point.Y - Display.Y;
        column = (x > 53 ? 1 : 0) + (x > 107 ? 1 : 0);
        row = (y > 51 ? 1 : 0) + (y > 103 ? 1 : 0);
        return true;
    }

    /// <summary>The sector under a point of the display, the centre included, when it is on the map.</summary>
    public static bool TrySectorAt(Point point, int centerSectorId, out int sectorId)
    {
        if (!TryCellAt(point, out var column, out var row)
            || SectorAt(centerSectorId, column, row) is not { } mapped)
        {
            sectorId = -1;
            return false;
        }
        sectorId = mapped;
        return true;
    }

    /// <summary>The cell of the nine-sector display that shows <paramref name="sectorId"/>.</summary>
    public static Rectangle? CellOf(int centerSectorId, int sectorId) =>
        Offset(centerSectorId, sectorId) is var (column, row) ? Cell(column, row) : null;

    /// <summary>The lightened inside of <paramref name="sectorId"/>'s cell (FND-UI-018).</summary>
    public static Rectangle? CellInteriorOf(int centerSectorId, int sectorId) =>
        Offset(centerSectorId, sectorId) is var (column, row) ? CellInterior(column, row) : null;

    /// <summary>The gang-status marker of a sector the display shows, where the map puts it.</summary>
    public static Rectangle? Marker(int centerSectorId, int sectorId) =>
        CellOf(centerSectorId, sectorId) is { } cell
            ? new Rectangle(cell.X + GangStatusMarkerLayout.CellOffset.X,
                cell.Y + GangStatusMarkerLayout.CellOffset.Y, 20, 20)
            : null;

    /// <summary>
    /// FND-UI-038: the labels <c>fn_00411119</c> puts on the display's frame, the centre's row and
    /// column always and a neighbour's when it is on the map.
    /// </summary>
    public static IEnumerable<GridLabel> DisplayLabels(int centerSectorId)
    {
        _ = CityMapLayout.Source(centerSectorId);
        var column = centerSectorId % 8;
        var row = centerSectorId / 8;
        var rowTab = new Rectangle(348, 448, 13, 23);
        var columnTab = new Rectangle(325, 448, 23, 13);
        for (var offset = -1; offset <= 1; offset++)
        {
            if (row + offset is >= 0 and < 8)
                yield return new GridLabel(new Point(Display.X + 1, Display.Y + 66 + 52 * offset),
                    rowTab, new Point(1, 8), (row + offset + 1).ToString());
            if (column + offset is >= 0 and < 8)
                yield return new GridLabel(new Point(Display.X + 69 + 54 * offset, Display.Y + 1),
                    columnTab, new Point(9, 1), ((char)('A' + column + offset)).ToString());
        }
    }

    public static Rectangle SitePortrait(int slot)
    {
        if (slot is < 0 or >= MatchLimits.SitesPerSector)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(86, 228 + slot * 66, 120, 64);
    }

    public static Rectangle SitePortraitFrameSource => new(0, 171, 120, 64);

    /// <summary>FND-UI-015: a double-click in <c>(86,228,120,196)</c> takes site <c>(y &gt; 294) + (y &gt; 360)</c>.</summary>
    public static int SiteAt(Point point)
    {
        if (!new Rectangle(86, 228, 120, 196).Contains(point)) return -1;
        return (point.Y > 294 ? 1 : 0) + (point.Y > 360 ? 1 : 0);
    }

    private static (int Column, int Row)? Offset(int centerSectorId, int sectorId)
    {
        _ = CityMapLayout.Source(centerSectorId);
        _ = CityMapLayout.Source(sectorId);
        var deltaColumn = sectorId % 8 - centerSectorId % 8;
        var deltaRow = sectorId / 8 - centerSectorId / 8;
        if (deltaColumn is < -1 or > 1 || deltaRow is < -1 or > 1) return null;
        return (deltaColumn + 1, deltaRow + 1);
    }

    public static Rectangle SiteControlBar(int slot)
    {
        var portrait = SitePortrait(slot);
        return new Rectangle(portrait.X + 10, portrait.Y + 59, 100, 3);
    }
}
