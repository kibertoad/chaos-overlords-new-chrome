using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class MovementUiTests
{
    [Fact]
    public void LayoutUsesExactNativeThreeByThreeSectorTiles()
    {
        // SCR-MOVE-001 mouse input.
        Assert.Equal(new Rectangle(104, 124, 344, 209), MovementLayout.Panel);
        Assert.Equal(new Rectangle(236, 150, 162, 156), MovementLayout.Neighborhood);
        Assert.Equal(new Rectangle(236, 150, 54, 52), MovementLayout.Cell(0, 0));
        Assert.Equal(new Rectangle(290, 202, 54, 52), MovementLayout.Cell(1, 1));
        Assert.Equal(new Rectangle(344, 254, 54, 52), MovementLayout.Cell(2, 2));
        Assert.True(MovementLayout.IsDestinationCell(0, 0));
        Assert.False(MovementLayout.IsDestinationCell(1, 1));
        Assert.True(MovementLayout.IsDestinationCell(2, 2));
        Assert.Equal(new Rectangle(137, 261, 49, 22), MovementLayout.Cancel);
        Assert.Equal(new Rectangle(137, 293, 49, 22), MovementLayout.Ok);
    }

    [Fact]
    public void NeighborhoodMapsAllEightAdjacentSectorsAroundCenter()
    {
        var sectors = Enumerable.Range(0, 3)
            .SelectMany(column => Enumerable.Range(0, 3)
                .Select(row => MovementLayout.SectorAt(27, column, row)))
            .ToArray();

        Assert.Equal([18, 26, 34, 19, 27, 35, 20, 28, 36], sectors);
        Assert.Equal((0, 0), MovementLayout.PositionOf(27, 18));
        Assert.Equal((2, 2), MovementLayout.PositionOf(27, 36));
    }

    [Fact]
    public void NeighborhoodMarksCellsBeyondCityEdgesUnavailable()
    {
        Assert.Equal(-1, MovementLayout.SectorAt(0, 0, 0));
        Assert.Equal(-1, MovementLayout.SectorAt(0, 1, 0));
        Assert.Equal(-1, MovementLayout.SectorAt(0, 0, 1));
        Assert.Equal(1, MovementLayout.SectorAt(0, 2, 1));
        Assert.Equal(8, MovementLayout.SectorAt(0, 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => MovementLayout.Cell(3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MovementLayout.IsDestinationCell(0, 3));
    }

    [Fact]
    public void NeighborhoodIsACropOfTheDrawnCityMap()
    {
        // FND-MOVE-004: corner (5 + 53 * column - 55, 4 + 51 * row - 53), so the gang's own map
        // cell (5 + 53 * column, 4 + 51 * row) lands at (55, 53) of the copy.
        Assert.Equal(new Rectangle(109, 104, 162, 156), MovementLayout.NeighborhoodSource(27));
        Assert.Equal(new Rectangle(-50, -49, 162, 156), MovementLayout.NeighborhoodSource(0));
        var crop = MovementLayout.NeighborhoodSource(27);
        var ownCell = CityMapLayout.OwnershipSource(27);
        Assert.Equal(new Point(55, 53), new Point(ownCell.X - crop.X, ownCell.Y - crop.Y));
    }

    [Fact]
    public void OffCityBandsCoverTheCellsBeyondEachEdge()
    {
        // FND-MOVE-004.
        Assert.Empty(MovementLayout.OffCityBands(27));
        Assert.Equal([new Rectangle(236, 150, 162, 52), new Rectangle(236, 150, 54, 156)],
            MovementLayout.OffCityBands(0));
        Assert.Equal([new Rectangle(236, 254, 162, 52), new Rectangle(344, 150, 54, 156)],
            MovementLayout.OffCityBands(63));
    }

    [Fact]
    public void ChosenDirectionIsMarkedByItsArrow()
    {
        // SCR-MOVE-001, FND-MOVE-005: index i of the offsets -9, -8, -7, -1, +1, +7, +8, +9.
        Assert.Equal(0, MovementLayout.DirectionIndex(0, 0));
        Assert.Equal(3, MovementLayout.DirectionIndex(0, 1));
        Assert.Equal(-1, MovementLayout.DirectionIndex(1, 1));
        Assert.Equal(4, MovementLayout.DirectionIndex(2, 1));
        Assert.Equal(7, MovementLayout.DirectionIndex(2, 2));
        Assert.Equal(new Rectangle(273, 185, 32, 32), MovementLayout.Arrow(0));
        Assert.Equal(new Rectangle(302, 177, 32, 32), MovementLayout.Arrow(1));
        Assert.Equal(new Rectangle(265, 212, 32, 32), MovementLayout.Arrow(3));
        Assert.Equal(new Rectangle(337, 212, 32, 32), MovementLayout.Arrow(4));
        Assert.Equal(new Rectangle(302, 246, 32, 32), MovementLayout.Arrow(6));
        Assert.Equal(new Rectangle(329, 238, 32, 32), MovementLayout.Arrow(7));
        Assert.Equal(new Rectangle(224, 448, 32, 32), MovementLayout.ArrowSource(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => MovementLayout.Arrow(8));
    }
}
