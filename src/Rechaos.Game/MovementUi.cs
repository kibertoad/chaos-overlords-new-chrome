using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class MovementLayout
{
    public const int Columns = 3;
    public const int Rows = 3;
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Portrait => EquipmentCommandLayout.Portrait;
    public static Rectangle Cancel => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public static Rectangle Neighborhood => new(236, 151, 162, 156);

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
    private void HandleMovementCommandClick(Point point)
    {
        if (MovementLayout.Cancel.Contains(point))
        {
            AcceptInput();
            BackFromCommands();
            return;
        }
        if (MovementLayout.Ok.Contains(point))
        {
            ActivateCommandSelection();
            return;
        }
        if (_state is null || _commandTargetOptions.Count == 0) return;
        var actor = _state.FindGang(_commandTargetOptions[0].Gang)!;
        for (var column = 0; column < MovementLayout.Columns; column++)
        for (var row = 0; row < MovementLayout.Rows; row++)
        {
            if (!MovementLayout.Cell(column, row).Contains(point)) continue;
            SelectMovementSector(MovementLayout.SectorAt(actor.SectorId, column, row));
            return;
        }
        if (!MovementLayout.Panel.Contains(point)) BackFromCommands();
    }

    private void MoveMovementTarget(int deltaX, int deltaY)
    {
        if (_state is null || _commandTargetOptions.Count == 0) return;
        var actor = _state.FindGang(_commandTargetOptions[0].Gang)!;
        var selected = _commandTargetOptions[_commandTargetCursor].Target.Id;
        var position = MovementLayout.PositionOf(actor.SectorId, selected);
        var column = position.Column + deltaX;
        var row = position.Row + deltaY;
        if (column is < 0 or >= MovementLayout.Columns
            || row is < 0 or >= MovementLayout.Rows) return;
        SelectMovementSector(MovementLayout.SectorAt(actor.SectorId, column, row));
    }

    private void SelectMovementSector(int sectorId)
    {
        for (var index = 0; index < _commandTargetOptions.Count; index++)
        {
            if (_commandTargetOptions[index].Target.Id != sectorId) continue;
            _commandTargetCursor = index;
            return;
        }
    }

    private void DrawMovementCommandTargets(
        SpriteBatch batch,
        Texture2D pixel,
        MatchState state)
    {
        if (_movementBackground is not null)
            batch.Draw(_movementBackground, MovementLayout.Panel, Color.White);
        else
            batch.Draw(pixel, MovementLayout.Panel, new Color(0, 0, 0, 248));
        var actor = state.FindGang(_commandTargetOptions[0].Gang)!;
        if (_gangPortraits is not null)
            batch.Draw(_gangPortraits, MovementLayout.Portrait,
                OriginalSpriteLayout.GangPortrait(actor.DefinitionId), Color.White);

        for (var column = 0; column < MovementLayout.Columns; column++)
        for (var row = 0; row < MovementLayout.Rows; row++)
        {
            var sectorId = MovementLayout.SectorAt(actor.SectorId, column, row);
            if (sectorId < 0) continue;
            var sector = state.Sectors[sectorId];
            var destination = MovementLayout.Cell(column, row);
            var layer = _cityOwnershipLayers[CityMapLayout.OwnershipSheet(sector.Owner)];
            if (layer is not null)
                batch.Draw(layer, destination, CityMapLayout.Source(sectorId), Color.White);
            else
                batch.Draw(pixel, destination, sector.Owner is { } owner
                    ? PlayerColors[owner.Value] * .68f
                    : new Color(24, 37, 39));
            if (sectorId == actor.SectorId)
                DrawBorder(batch, pixel, destination, Color.Gold, 1);
        }

        var selected = _commandTargetOptions[_commandTargetCursor].Target.Id;
        var selectedPosition = MovementLayout.PositionOf(actor.SectorId, selected);
        DrawBorder(batch, pixel,
            MovementLayout.Cell(selectedPosition.Column, selectedPosition.Row), Color.White, 2);
    }
}
