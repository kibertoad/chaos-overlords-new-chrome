using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatResultsLayoutTests
{
    [Fact]
    public void PointerTargetsFollowRecoveredCombatResultsHandler()
    {
        Assert.Equal(new Rectangle(135, 157, 26, 23), CombatResultsLayout.Previous);
        Assert.Equal(new Rectangle(163, 157, 26, 23), CombatResultsLayout.Next);
        Assert.Equal(new Rectangle(137, 293, 49, 22), CombatResultsLayout.Ok);

        Assert.Equal(0, CombatResultsLayout.FriendlyForceSlotAt(new Point(205, 152)));
        Assert.Equal(3, CombatResultsLayout.FriendlyForceSlotAt(new Point(250, 250)));
        Assert.Null(CombatResultsLayout.FriendlyForceSlotAt(new Point(201, 152)));
    }
}
