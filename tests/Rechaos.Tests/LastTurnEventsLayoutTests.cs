using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class LastTurnEventsLayoutTests
{
    [Fact]
    public void PointerTargetsFollowRecoveredLastTurnEventsHandler()
    {
        Assert.Equal(new Rectangle(135, 157, 26, 23), LastTurnEventsLayout.Previous);
        Assert.Equal(new Rectangle(163, 157, 26, 23), LastTurnEventsLayout.Next);
        Assert.Equal(new Rectangle(137, 293, 49, 22), LastTurnEventsLayout.Ok);
    }
}
