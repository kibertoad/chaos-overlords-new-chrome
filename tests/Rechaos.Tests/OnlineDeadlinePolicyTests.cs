using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class OnlineDeadlinePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SealedTurnsDeadlineIsNotTakenForTheNewTurnRunningOut()
    {
        var sealedTurn = Now.AddSeconds(-20);
        var setAside = OnlineDeadlinePolicy.SetAside(sealedTurn, Now);

        Assert.Equal(sealedTurn, setAside);
        Assert.False(OnlineDeadlinePolicy.HasPassed(sealedTurn, setAside, Now));
        // The new turn's own deadline, once the stream delivers it and it runs out, still counts.
        var newTurn = Now.AddSeconds(30);
        Assert.False(OnlineDeadlinePolicy.HasPassed(newTurn, setAside, Now));
        Assert.True(OnlineDeadlinePolicy.HasPassed(newTurn, setAside, Now.AddSeconds(31)));
    }

    [Fact]
    public void DeadlineStillAheadWhenTheTurnOpensIsNeverSetAside()
    {
        // Delivered before the adopt, so it is the new turn's own.
        var newTurn = Now.AddSeconds(30);

        Assert.Null(OnlineDeadlinePolicy.SetAside(newTurn, Now));
        Assert.Null(OnlineDeadlinePolicy.SetAside(null, Now));
        Assert.True(OnlineDeadlinePolicy.HasPassed(newTurn, null, Now.AddSeconds(30)));
        Assert.False(OnlineDeadlinePolicy.HasPassed(null, null, Now));
    }
}
