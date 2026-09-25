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
    public void HeldItemBoxesSitBeneathThePortraitAboveCancel()
    {
        var portrait = EquipmentCommandLayout.Portrait;
        Assert.Equal(3, EquipmentCommandLayout.EquippedItemCount);
        for (var slot = 0; slot < EquipmentCommandLayout.EquippedItemCount; slot++)
        {
            var box = EquipmentCommandLayout.EquippedItem(slot);
            Assert.True(box.Y >= portrait.Bottom);
            Assert.True(box.Bottom < EquipmentCommandLayout.Cancel.Y);
            Assert.InRange(box.X, portrait.X, portrait.Right - box.Width);
        }
        Assert.Equal(portrait.X, EquipmentCommandLayout.EquippedItem(0).X);
        Assert.Equal(portrait.Right, EquipmentCommandLayout.EquippedItem(2).Right);
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentCommandLayout.EquippedItem(3));
    }

    [Fact]
    public void OriginalListTargetsUseSixteenNinePixelRows()
    {
        Assert.Equal(16, EquipmentCommandLayout.VisibleItemCount);
        Assert.Equal(new Rectangle(208, 140, 32, 32), EquipmentCommandLayout.CategoryHit(0));
        Assert.Equal(new Rectangle(208, 248, 32, 32), EquipmentCommandLayout.CategoryHit(3));
        Assert.Equal(new Rectangle(252, 150, 180, 143), EquipmentCommandLayout.ItemListHit);
        Assert.Equal(new Rectangle(252, 143, 180, 143), EquipmentCommandLayout.ResearchItemDetailHit);
        Assert.Equal(new Rectangle(251, 149, 181, 9), EquipmentCommandLayout.ItemRow(0));
        Assert.Equal(new Rectangle(251, 284, 181, 9), EquipmentCommandLayout.ItemRow(15));
        Assert.Equal(0, EquipmentCommandLayout.ItemRowAt(new Point(252, 150)));
        Assert.Equal(15, EquipmentCommandLayout.ItemRowAt(new Point(252, 292)));
        Assert.Equal(-1, EquipmentCommandLayout.ItemRowAt(new Point(252, 293)));
        // SCR-RESEARCH-001: a press above panel y 26 selects nothing, and a double-click there
        // opens the first row.
        Assert.Equal(-1, EquipmentCommandLayout.ItemRowAt(new Point(252, 143)));
        Assert.Equal(0, EquipmentCommandLayout.ResearchItemDetailRowAt(new Point(252, 143)));
        Assert.Equal(15, EquipmentCommandLayout.ResearchItemDetailRowAt(new Point(252, 285)));
        Assert.Equal(-1, EquipmentCommandLayout.ResearchItemDetailRowAt(new Point(252, 286)));
    }
}
