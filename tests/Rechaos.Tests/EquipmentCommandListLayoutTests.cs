using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class EquipmentCommandListLayoutTests
{
    [Theory]
    [InlineData(8, 0)]
    [InlineData(8, 6)]
    [InlineData(16, 15)]
    public void CategoriesThatFitAlwaysKeepTheirFirstItemVisible(int itemCount, int selected)
    {
        Assert.Equal(0, EquipmentCommandLayout.FirstVisibleItem(itemCount, selected));
    }

    [Fact]
    public void OriginalListTargetsUseSixteenNinePixelRows()
    {
        Assert.Equal(16, EquipmentCommandLayout.VisibleItemCount);
        Assert.Equal(new Rectangle(208, 140, 32, 32), EquipmentCommandLayout.CategoryHit(0));
        Assert.Equal(new Rectangle(208, 248, 32, 32), EquipmentCommandLayout.CategoryHit(3));
        Assert.Equal(new Rectangle(252, 150, 180, 143), EquipmentCommandLayout.ItemListHit);
        Assert.Equal(new Rectangle(252, 143, 180, 143), EquipmentCommandLayout.ResearchItemListHit);
        Assert.Equal(new Rectangle(251, 149, 181, 9), EquipmentCommandLayout.ItemRow(0));
        Assert.Equal(new Rectangle(251, 284, 181, 9), EquipmentCommandLayout.ItemRow(15));
        Assert.Equal(0, EquipmentCommandLayout.ItemRowAt(new Point(252, 150)));
        Assert.Equal(15, EquipmentCommandLayout.ItemRowAt(new Point(252, 292)));
        Assert.Equal(-1, EquipmentCommandLayout.ItemRowAt(new Point(252, 293)));
        Assert.Equal(0, EquipmentCommandLayout.ResearchItemRowAt(new Point(252, 143)));
        Assert.Equal(15, EquipmentCommandLayout.ResearchItemRowAt(new Point(252, 285)));
        Assert.Equal(-1, EquipmentCommandLayout.ResearchItemRowAt(new Point(252, 286)));
    }
}
