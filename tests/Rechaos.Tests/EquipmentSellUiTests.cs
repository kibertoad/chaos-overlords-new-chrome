using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class EquipmentSellUiTests
{
    [Fact]
    public void LayoutMatchesOriginalEquipmentToSellPanel()
    {
        // SCR-SELL-001 drawn elements and mouse input.
        Assert.Equal(new Rectangle(104, 124, 344, 209), EquipmentSellLayout.Panel);
        Assert.Equal(new Rectangle(130, 141, 64, 64), EquipmentSellLayout.Portrait);
        Assert.Equal(new Rectangle(217, 141, 48, 48), EquipmentSellLayout.ItemPicture(0));
        Assert.Equal(new Rectangle(217, 269, 48, 48), EquipmentSellLayout.ItemPicture(2));
        Assert.Equal(new Point(272, 156), EquipmentSellLayout.NameOrigin(0));
        Assert.Equal(new Point(272, 284), EquipmentSellLayout.NameOrigin(2));
        Assert.Equal(new Point(386, 174), EquipmentSellLayout.PriceField(0));
        Assert.Equal(new Point(386, 302), EquipmentSellLayout.PriceField(2));
        Assert.Equal(new Rectangle(272, 149, 125, 32), EquipmentSellLayout.EmptyRow(0));
        Assert.Equal(new Rectangle(272, 277, 125, 32), EquipmentSellLayout.EmptyRow(2));
        Assert.Equal(new Rectangle(214, 138, 192, 54), EquipmentSellLayout.Highlight(0));
        Assert.Equal(new Rectangle(214, 266, 192, 54), EquipmentSellLayout.Highlight(2));
        Assert.Equal(new Rectangle(222, 363, 192, 54), EquipmentSellLayout.HighlightSource);
        Assert.Equal(new Rectangle(215, 139, 190, 52), EquipmentSellLayout.ItemHit(0));
        Assert.Equal(new Rectangle(215, 267, 190, 52), EquipmentSellLayout.ItemHit(2));
        Assert.Equal(new Rectangle(137, 261, 49, 22), EquipmentSellLayout.Cancel);
        Assert.Equal(new Rectangle(137, 293, 49, 22), EquipmentSellLayout.Ok);
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentSellLayout.Highlight(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentSellLayout.ItemHit(3));
    }

    [Fact]
    public void HighlightLiesOnePixelOutsideEachRowTarget()
    {
        // FND-SELL-002.
        for (var slot = 0; slot < 3; slot++)
        {
            var hit = EquipmentSellLayout.ItemHit(slot);
            var highlight = EquipmentSellLayout.Highlight(slot);
            Assert.Equal(new Rectangle(hit.X - 1, hit.Y - 1, hit.Width + 2, hit.Height + 2), highlight);
        }
    }

    [Fact]
    public void ExistingSellOrderRestoresItsSelection()
    {
        var queued = EquipmentSellSelection.CreateCommand(new PlayerId(0), new GangId(1), [(short)40]);

        Assert.Equal([false, false, true], EquipmentSellSelection.OpeningSelection(queued, [12, null, 40]));
        Assert.Equal([false, false, false], EquipmentSellSelection.OpeningSelection(null, [12, null, 40]));
    }

    [Fact]
    public void SelectionBuildsOneExactThreeItemCommand()
    {
        var command = EquipmentSellSelection.CreateCommand(
            new PlayerId(2), new GangId(17), [(short)4, (short)31, (short)44], repeat: true);

        Assert.Equal(GangAction.Sell, command.Action);
        Assert.Equal(CommandTarget.Item(4), command.Target);
        Assert.Equal(CommandTarget.Item(31), command.SecondaryTarget);
        Assert.Equal(CommandTarget.Item(44), command.TertiaryTarget);
        Assert.True(command.Repeat);
    }

    [Fact]
    public void SelectionRequiresOneToThreeDistinctItems()
    {
        Assert.Throws<ArgumentException>(() => EquipmentSellSelection.CreateCommand(
            new PlayerId(0), new GangId(0), []));
        Assert.Throws<ArgumentException>(() => EquipmentSellSelection.CreateCommand(
            new PlayerId(0), new GangId(0), [(short)0, (short)1, (short)2, (short)3]));
        var deduplicated = EquipmentSellSelection.CreateCommand(
            new PlayerId(0), new GangId(0), [(short)4, (short)4]);
        Assert.Null(deduplicated.SecondaryTarget);
    }
}
