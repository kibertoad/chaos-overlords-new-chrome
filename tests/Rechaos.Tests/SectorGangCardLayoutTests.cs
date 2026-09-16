using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SectorGangCardLayoutTests
{
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
