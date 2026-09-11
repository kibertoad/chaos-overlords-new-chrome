using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class MovementUiTests
{
    [Fact]
    public void LayoutUsesExactNativeThreeByThreeSectorTiles()
    {
        Assert.Equal(new Rectangle(104, 125, 344, 209), MovementLayout.Panel);
        Assert.Equal(new Rectangle(236, 151, 162, 156), MovementLayout.Neighborhood);
        Assert.Equal(new Rectangle(236, 151, 54, 52), MovementLayout.Cell(0, 0));
        Assert.Equal(new Rectangle(344, 255, 54, 52), MovementLayout.Cell(2, 2));
        Assert.Equal(EquipmentCommandLayout.Cancel, MovementLayout.Cancel);
        Assert.Equal(EquipmentCommandLayout.Ok, MovementLayout.Ok);
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
    }
}
