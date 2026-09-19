using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatPanelLayoutTests
{
    [Fact]
    public void EquipmentAperturesMatchTheNativeDetailedCombatPanel()
    {
        Assert.Equal(new Rectangle(204, 172, 48, 48), CombatPanelLayout.EquipmentItem(false, 0));
        Assert.Equal(new Rectangle(204, 221, 48, 48), CombatPanelLayout.EquipmentItem(false, 1));
        Assert.Equal(new Rectangle(204, 270, 48, 48), CombatPanelLayout.EquipmentItem(false, 2));
        Assert.Equal(new Rectangle(393, 172, 48, 48), CombatPanelLayout.EquipmentItem(true, 0));
        Assert.Equal(new Rectangle(393, 221, 48, 48), CombatPanelLayout.EquipmentItem(true, 1));
        Assert.Equal(new Rectangle(393, 270, 48, 48), CombatPanelLayout.EquipmentItem(true, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatPanelLayout.EquipmentItem(false, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CombatPanelLayout.EquipmentItem(false, 3));
    }
}
