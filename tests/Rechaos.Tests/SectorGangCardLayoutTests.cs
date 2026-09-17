using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SectorGangCardLayoutTests
{
    [Fact]
    public void NativeAtlasAndDestinationGeometryAreExact()
    {
        Assert.Equal(new Rectangle(162, 15, 74, 110), OriginalSpriteLayout.GangCardFrame);
        Assert.Equal(new Rectangle(162, 125, 64, 9),
            OriginalSpriteLayout.GangActionStrip(GangAction.None));
        Assert.Equal(new Rectangle(162, 134, 64, 9),
            OriginalSpriteLayout.GangActionStrip(GangAction.Attack));
        Assert.Equal(new Rectangle(162, 251, 64, 9),
            OriginalSpriteLayout.GangActionStrip(GangAction.Terminate));
        Assert.Equal(new Rectangle(254, 80, 74, 110), SectorGangCardLayout.Frame(0));
        Assert.Equal(new Rectangle(330, 80, 74, 110), SectorGangCardLayout.Frame(1));
        Assert.Equal(new Rectangle(254, 192, 74, 110), SectorGangCardLayout.Frame(2));
        Assert.Equal(new Rectangle(330, 304, 74, 110), SectorGangCardLayout.Frame(5));
        Assert.Equal(new Rectangle(253, 79, 76, 112), SectorGangCardLayout.OwnerBorder(0));
        Assert.Equal(new Rectangle(261, 83, 60, 3), SectorGangCardLayout.ForceBar(0));
        Assert.Equal(0, SectorGangCardLayout.ForceWidth(0));
        Assert.Equal(6, SectorGangCardLayout.ForceWidth(1));
        Assert.Equal(60, SectorGangCardLayout.ForceWidth(10));
        Assert.Equal(new Rectangle(259, 88, 31, 9), SectorGangCardLayout.OneOffAction(0));
        Assert.Equal(new Rectangle(291, 88, 32, 9), SectorGangCardLayout.RepeatingAction(0));
        Assert.Equal(new Rectangle(259, 100, 64, 64), SectorGangCardLayout.Portrait(0));
        Assert.Equal(new Rectangle(303, 166, 20, 20), SectorGangCardLayout.ItemSlot(0, 2));
        Assert.Equal(new Rectangle(303, 166, 20, 20), SectorGangCardLayout.ItemPortrait(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangCardLayout.Frame(6));
    }

    [Fact]
    public void ActionHalfSelectsRepeatModeIndependentlyOfAssignedCommand()
    {
        Assert.False(SectorGangCardLayout.ActionRepeatAt(
            0, SectorGangCardLayout.OneOffAction(0).Center));
        Assert.True(SectorGangCardLayout.ActionRepeatAt(
            0, SectorGangCardLayout.RepeatingAction(0).Center));
        Assert.Null(SectorGangCardLayout.ActionRepeatAt(
            0, SectorGangCardLayout.Portrait(0).Center));
    }
}
