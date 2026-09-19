using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangInformationEquipmentTests
{
    [Fact]
    public void EquippedItemTargetsRecognizeOnlyTheThreePortraitApertures()
    {
        for (var slot = 0; slot < 3; slot++)
            Assert.Equal(slot, GangInformationLayout.EquipmentSlotAt(
                GangInformationLayout.Equipment(slot).Center));

        Assert.Null(GangInformationLayout.EquipmentSlotAt(new Point(399, 195)));
        Assert.Null(GangInformationLayout.EquipmentSlotAt(new Point(448, 165)));
    }
}
