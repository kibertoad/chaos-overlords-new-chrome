using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class AttackCommandLayoutTests
{
    [Fact]
    public void PointerTargetsFollowRecoveredAttackHandlerGrid()
    {
        Assert.Equal(new Rectangle(202, 140, 32, 32), AttackCommandLayout.Opponent(0));
        Assert.Equal(new Rectangle(202, 284, 32, 32), AttackCommandLayout.Opponent(4));

        Assert.Equal(new Rectangle(239, 140, 67, 89), AttackCommandLayout.TargetHit(0));
        Assert.Equal(new Rectangle(306, 140, 68, 89), AttackCommandLayout.TargetHit(1));
        Assert.Equal(new Rectangle(374, 140, 67, 89), AttackCommandLayout.TargetHit(2));
        Assert.Equal(new Rectangle(239, 229, 67, 88), AttackCommandLayout.TargetHit(3));
        Assert.Equal(new Rectangle(374, 229, 67, 88), AttackCommandLayout.TargetHit(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => AttackCommandLayout.TargetHit(6));
    }
}
