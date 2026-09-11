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
        Assert.Equal(new Rectangle(104, 125, 344, 209), EquipmentSellLayout.Panel);
        Assert.Equal(new Rectangle(130, 143, 64, 64), EquipmentSellLayout.Portrait);
        Assert.Equal(new Rectangle(212, 141, 220, 51), EquipmentSellLayout.ItemRow(0));
        Assert.Equal(new Rectangle(212, 269, 220, 51), EquipmentSellLayout.ItemRow(2));
        Assert.Equal(EquipmentCommandLayout.Cancel, EquipmentSellLayout.Cancel);
        Assert.Equal(EquipmentCommandLayout.Ok, EquipmentSellLayout.Ok);
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentSellLayout.ItemRow(3));
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
