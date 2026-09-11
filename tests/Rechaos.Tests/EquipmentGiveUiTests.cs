using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class EquipmentGiveUiTests
{
    [Fact]
    public void LayoutMatchesOriginalEquipmentToGivePanel()
    {
        Assert.Equal(new Rectangle(104, 125, 344, 209), EquipmentGiveLayout.Panel);
        Assert.Equal(new Rectangle(130, 142, 64, 64), EquipmentGiveLayout.Portrait);
        Assert.Equal(new Rectangle(208, 141, 50, 51), EquipmentGiveLayout.Item(0));
        Assert.Equal(new Rectangle(208, 269, 50, 51), EquipmentGiveLayout.Item(2));
        Assert.Equal(EquipmentCommandLayout.Cancel, EquipmentGiveLayout.Cancel);
        Assert.Equal(EquipmentCommandLayout.Ok, EquipmentGiveLayout.Ok);
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentGiveLayout.Item(3));
    }

    [Fact]
    public void SelectionBuildsOneRecipientAndThreeExactItemTargets()
    {
        var command = EquipmentGiveSelection.CreateCommand(
            new PlayerId(2), new GangId(17), new GangId(18),
            [(short)4, (short)31, (short)44], repeat: true);

        Assert.Equal(GangAction.Give, command.Action);
        Assert.Equal(CommandTarget.Gang(new GangId(18)), command.Target);
        Assert.Equal(CommandTarget.Item(4), command.SecondaryTarget);
        Assert.Equal(CommandTarget.Item(31), command.TertiaryTarget);
        Assert.Equal(CommandTarget.Item(44), command.QuaternaryTarget);
        Assert.True(command.Repeat);
    }

    [Fact]
    public void SelectionRequiresOneToThreeDistinctItems()
    {
        Assert.Throws<ArgumentException>(() => EquipmentGiveSelection.CreateCommand(
            new PlayerId(0), new GangId(0), new GangId(1), []));
        Assert.Throws<ArgumentException>(() => EquipmentGiveSelection.CreateCommand(
            new PlayerId(0), new GangId(0), new GangId(1), [(short)0, 1, 2, 3]));
        var deduplicated = EquipmentGiveSelection.CreateCommand(
            new PlayerId(0), new GangId(0), new GangId(1), [(short)4, 4]);
        Assert.Null(deduplicated.TertiaryTarget);
    }
}
