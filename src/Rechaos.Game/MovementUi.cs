using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class MovementLayout
{
    public const int Columns = 3;
    public const int Rows = 3;
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Portrait => EquipmentCommandLayout.Portrait;
    public static Rectangle Cancel => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public static Rectangle Neighborhood => SharedPanelLayout.At(132, 26, 162, 156);

    public static Rectangle Cell(int column, int row)
    {
        if (column is < 0 or >= Columns) throw new ArgumentOutOfRangeException(nameof(column));
        if (row is < 0 or >= Rows) throw new ArgumentOutOfRangeException(nameof(row));
        return new Rectangle(
            Neighborhood.X + column * CityMapLayout.TileWidth,
            Neighborhood.Y + row * CityMapLayout.TileHeight,
            CityMapLayout.TileWidth,
            CityMapLayout.TileHeight);
    }

    /// <summary>
    /// Native Move handler 0x004413ef maps the 3-by-3 neighborhood in row
    /// order but excludes its center before it considers a destination.
    /// </summary>
    public static bool IsDestinationCell(int column, int row)
    {
        _ = Cell(column, row);
        return column != 1 || row != 1;
    }

    public static int SectorAt(int centerSector, int column, int row)
    {
        if (centerSector is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(centerSector));
        _ = Cell(column, row);
        var x = centerSector % MatchLimits.BoardWidth + column - 1;
        var y = centerSector / MatchLimits.BoardWidth + row - 1;
        return x is < 0 or >= MatchLimits.BoardWidth || y is < 0 or >= MatchLimits.BoardWidth
            ? -1
            : y * MatchLimits.BoardWidth + x;
    }

    /// <summary>
    /// The 162-by-156 area of the drawn city map the panel copies to <see cref="Neighborhood"/>:
    /// its corner is (5 + 53 * column - 55, 4 + 51 * row - 53) for the gang's sector, so the
    /// gang's own map cell starts one pixel right of and below the middle grid cell's corner
    /// (SCR-MOVE-001, FND-MOVE-004). Parts beyond the map are covered by the off-city bands.
    /// </summary>
    public static Rectangle NeighborhoodSource(int centerSector)
    {
        ValidateSector(centerSector);
        var column = centerSector % MatchLimits.BoardWidth;
        var row = centerSector / MatchLimits.BoardWidth;
        return new Rectangle(
            5 + CityMapLayout.ColumnStride * column - 55,
            4 + CityMapLayout.RowStride * row - 53,
            Neighborhood.Width,
            Neighborhood.Height);
    }

    /// <summary>
    /// The black bands over grid cells beyond the city's edge: the top or bottom 52 rows when
    /// the sector is in row 0 or 7, the left or right 54 columns in column 0 or 7
    /// (SCR-MOVE-001, FND-MOVE-004).
    /// </summary>
    public static IReadOnlyList<Rectangle> OffCityBands(int centerSector)
    {
        ValidateSector(centerSector);
        var column = centerSector % MatchLimits.BoardWidth;
        var row = centerSector / MatchLimits.BoardWidth;
        var area = Neighborhood;
        var bands = new List<Rectangle>(2);
        if (row == 0) bands.Add(new Rectangle(area.X, area.Y, area.Width, CityMapLayout.TileHeight));
        if (row == MatchLimits.BoardWidth - 1)
            bands.Add(new Rectangle(area.X, area.Y + 2 * CityMapLayout.TileHeight, area.Width,
                CityMapLayout.TileHeight));
        if (column == 0) bands.Add(new Rectangle(area.X, area.Y, CityMapLayout.TileWidth, area.Height));
        if (column == MatchLimits.BoardWidth - 1)
            bands.Add(new Rectangle(area.X + 2 * CityMapLayout.TileWidth, area.Y, CityMapLayout.TileWidth,
                area.Height));
        return bands;
    }

    /// <summary>
    /// Index 0 to 7 of the offsets -9, -8, -7, -1, +1, +7, +8 and +9 for a neighbourhood cell,
    /// in row-major order without the centre; -1 for the centre (FND-MOVE-004).
    /// </summary>
    public static int DirectionIndex(int column, int row)
    {
        _ = Cell(column, row);
        var cell = column + Columns * row;
        return cell == 4 ? -1 : cell < 4 ? cell : cell - 1;
    }

    /// <summary>Screen corners of the eight direction arrows around the centre cell (FND-MOVE-005).</summary>
    private static readonly Point[] ArrowCorners =
    [
        new(273, 185), new(302, 177), new(329, 185), new(265, 212),
        new(337, 212), new(273, 238), new(302, 246), new(329, 238)
    ];

    public static Rectangle Arrow(int direction)
    {
        if (direction is < 0 or >= 8) throw new ArgumentOutOfRangeException(nameof(direction));
        return new Rectangle(ArrowCorners[direction], new Point(32, 32));
    }

    /// <summary>PX00129 cell of an arrow, keyed on exact white (SCR-MOVE-001, FND-MOVE-005).</summary>
    public static Rectangle ArrowSource(int direction)
    {
        if (direction is < 0 or >= 8) throw new ArgumentOutOfRangeException(nameof(direction));
        return new Rectangle(32 * direction, 448, 32, 32);
    }

    private static void ValidateSector(int sectorId)
    {
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
    }

    public static (int Column, int Row) PositionOf(int centerSector, int sector)
    {
        if (sector is < 0 or >= MatchLimits.SectorCount) throw new ArgumentOutOfRangeException(nameof(sector));
        var centerX = centerSector % MatchLimits.BoardWidth;
        var centerY = centerSector / MatchLimits.BoardWidth;
        return (sector % MatchLimits.BoardWidth - centerX + 1,
            sector / MatchLimits.BoardWidth - centerY + 1);
    }
}

public sealed partial class ChaosGame
{
    /// <summary>
    /// SCR-MOVE-001: the panel opens with no destination, or with the stored one and the
    /// confirm face enabled when the gang already has a Move order (FND-MOVE-004).
    /// </summary>
    private void OpenMovementPanel()
    {
        _commandTargetCursor = -1;
        _commandPanelFace = CommandPanelFaceState.NotDrawn;
        if (_state?.FindGang(_commandTargetOptions[0].Gang)?.QueuedCommand?.Command is not
            { Action: GangAction.Move } queued) return;
        _commandPanelFace = CommandPanelFaces.OnOpening(true);
        SelectMovementSector(queued.Target.Id);
    }

    private void HandleMovementCommandClick(Point point)
    {
        if (CommandPanelFaces.ButtonAt(point) is { } button)
        {
            BeginCommandPanelButton(button);
            return;
        }
        if (!MovementLayout.Panel.Contains(point))
        {
            RejectOutsideCommandPanel();
            return;
        }
        if (_state is null || _commandTargetOptions.Count == 0) return;
        var actor = _state.FindGang(_commandTargetOptions[0].Gang)!;
        for (var column = 0; column < MovementLayout.Columns; column++)
        for (var row = 0; row < MovementLayout.Rows; row++)
        {
            if (!MovementLayout.Cell(column, row).Contains(point)) continue;
            if (!MovementLayout.IsDestinationCell(column, row)) return;
            var sector = MovementLayout.SectorAt(actor.SectorId, column, row);
            if (sector >= 0) SelectMovementSector(sector);
            return;
        }
    }

    private void MoveMovementTarget(int deltaX, int deltaY)
    {
        if (_state is null || _commandTargetOptions.Count == 0) return;
        var actor = _state.FindGang(_commandTargetOptions[0].Gang)!;
        var position = _commandTargetCursor < 0
            ? (Column: 1, Row: 1)
            : MovementLayout.PositionOf(actor.SectorId, _commandTargetOptions[_commandTargetCursor].Target.Id);
        var column = position.Column + deltaX;
        var row = position.Row + deltaY;
        if (column is < 0 or >= MovementLayout.Columns
            || row is < 0 or >= MovementLayout.Rows
            || !MovementLayout.IsDestinationCell(column, row)) return;
        var sector = MovementLayout.SectorAt(actor.SectorId, column, row);
        if (sector >= 0) SelectMovementSector(sector);
    }

    /// <summary>
    /// Chooses a destination the order may take; a cell without one does nothing, as a cell
    /// the original's table disables does (SCR-MOVE-001).
    /// </summary>
    private void SelectMovementSector(int sectorId)
    {
        for (var index = 0; index < _commandTargetOptions.Count; index++)
        {
            if (_commandTargetOptions[index].Target.Id != sectorId) continue;
            _commandTargetCursor = index;
            _commandPanelFace = CommandPanelFaces.AfterChange(true);
            return;
        }
    }

    private void DrawMovementCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state)
    {
        DrawPanelArtwork(batch, pixel, _movementBackground, MovementLayout.Panel, 248);
        var actor = state.FindGang(_commandTargetOptions[0].Gang)!;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, MovementLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(actor.DefinitionId), Color.White);

        DrawMovementNeighborhood(batch, pixel, state, actor.SectorId);

        if (_commandTargetCursor >= 0 && _uiKeyedSprites is not null)
        {
            var selected = _commandTargetOptions[_commandTargetCursor].Target.Id;
            var (column, row) = MovementLayout.PositionOf(actor.SectorId, selected);
            var direction = MovementLayout.DirectionIndex(column, row);
            if (direction >= 0)
                batch.Draw(_uiKeyedSprites, MovementLayout.Arrow(direction),
                    MovementLayout.ArrowSource(direction), Color.White);
        }
        DrawCommandPanelFaces(batch);
    }

    /// <summary>
    /// The crop of the drawn city map around the gang's sector, its black outline and the
    /// black bands beyond the city's edge (SCR-MOVE-001, FND-MOVE-004).
    /// </summary>
    private void DrawMovementNeighborhood(SpriteBatch batch, Texture2D pixel, MatchState state, int centerSector)
    {
        var area = MovementLayout.Neighborhood;
        var crop = MovementLayout.NeighborhoodSource(centerSector);
        var neutral = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(null)];
        if (neutral is not null)
            DrawCropped(batch, neutral, CityMapLayout.Bounds with { X = 0, Y = 0 }, crop, area);
        else
            batch.Draw(pixel, area, new Color(24, 37, 39));
        for (var sectorId = 0; sectorId < state.Sectors.Count; sectorId++)
        {
            var owner = state.Sectors[sectorId].Owner;
            var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(owner)];
            if (owner is null || layer is null) continue;
            DrawCropped(batch, layer, CityMapLayout.OwnershipSource(sectorId), crop, area);
        }
        DrawBorder(batch, pixel, area, Color.Black, 1);
        foreach (var band in MovementLayout.OffCityBands(centerSector))
            batch.Draw(pixel, band, Color.Black);
    }

    /// <summary>Draws the part of <paramref name="source"/> inside <paramref name="crop"/> at 1:1.</summary>
    private static void DrawCropped(
        SpriteBatch batch, Texture2D texture, Rectangle source, Rectangle crop, Rectangle destination)
    {
        var visible = Rectangle.Intersect(source, crop);
        if (visible.IsEmpty) return;
        batch.Draw(texture,
            new Rectangle(destination.X + visible.X - crop.X, destination.Y + visible.Y - crop.Y,
                visible.Width, visible.Height),
            visible, Color.White);
    }
}
