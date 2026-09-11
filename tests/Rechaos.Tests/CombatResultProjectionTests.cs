using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatResultProjectionTests
{
    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(0, 2, false)]
    [InlineData(2, 2, false)]
    public void OnlyTheImmediatelyCompletedTurnIsPresented(
        int eventTurn,
        int currentTurn,
        bool expected)
    {
        Assert.Equal(expected,
            CombatResultProjection.IsFromLastCompletedTurn(eventTurn, currentTurn));
    }

    [Fact]
    public void InvalidTurnValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatResultProjection.IsFromLastCompletedTurn(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatResultProjection.IsFromLastCompletedTurn(0, 0));
    }
}
