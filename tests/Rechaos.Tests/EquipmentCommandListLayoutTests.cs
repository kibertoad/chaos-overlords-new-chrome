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
    public void CarriedItemIconsSitUnderThePortrait()
    {
        // SCR-EQUIP-001, FND-EQUIP-010.
        Assert.Equal(3, EquipmentCommandLayout.EquippedItemCount);
        Assert.Equal(new Rectangle(130, 206, 20, 20), EquipmentCommandLayout.EquippedItem(0));
        Assert.Equal(new Rectangle(152, 206, 20, 20), EquipmentCommandLayout.EquippedItem(1));
        Assert.Equal(new Rectangle(174, 206, 20, 20), EquipmentCommandLayout.EquippedItem(2));
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

    [Fact]
    public void CategoryFrameLiesOnePixelOutsideEachCell()
    {
        // SCR-EQUIP-001, FND-EQUIP-009.
        Assert.Equal(new Rectangle(120, 171, 34, 34), EquipmentCommandLayout.CategoryFrameSource);
        for (var category = 0; category < EquipmentCommandLayout.CategoryCount; category++)
        {
            var cell = EquipmentCommandLayout.CategoryHit(category);
            Assert.Equal(new Rectangle(cell.X - 1, cell.Y - 1, cell.Width + 2, cell.Height + 2),
                EquipmentCommandLayout.Category(category));
        }
    }

    [Fact]
    public void ListTextAndPricesFollowTheOriginalColumns()
    {
        // SCR-EQUIP-001: names from (252, 150 + 9 * n), prices in two cells from x 420.
        Assert.Equal(new Point(252, 150), EquipmentCommandLayout.ItemNameOrigin(0));
        Assert.Equal(new Point(252, 285), EquipmentCommandLayout.ItemNameOrigin(15));
        Assert.Equal(420, EquipmentCommandLayout.PriceLeft("45"));
        Assert.Equal(426, EquipmentCommandLayout.PriceLeft("8"));
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentCommandLayout.PriceLeft("100"));
    }

    [Fact]
    public void ChosenRowUsesTheSecondFontRowPaddedToThirtyCharacters()
    {
        // SCR-EQUIP-001, FND-EQUIP-010.
        var text = EquipmentCommandLayout.ChosenRowText("KNIFE");
        Assert.Equal(30, text.Length);
        Assert.StartsWith("KNIFE ", text);
        Assert.Equal(30, EquipmentCommandLayout.ChosenRowText(new string('A', 40)).Length);
        Assert.Equal(new Rectangle(6 * ('K' - 32), 441, 6, 7), EquipmentCommandLayout.ChosenRowGlyphSource('K'));
        Assert.Equal(new Rectangle(0, 441, 6, 7), EquipmentCommandLayout.ChosenRowGlyphSource(' '));
        Assert.Equal(new Color(0, 255, 0), EquipmentCommandLayout.ChosenRowFrame);
    }
}
