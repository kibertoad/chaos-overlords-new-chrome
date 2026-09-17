using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class GangStatusMarkerPresentationTests
{
    [Theory]
    [InlineData(false, false, false, 0)]
    [InlineData(false, true, false, 1)]
    [InlineData(true, false, false, 2)]
    [InlineData(true, true, false, 3)]
    [InlineData(false, false, true, 4)]
    [InlineData(false, true, true, 5)]
    [InlineData(true, false, true, 6)]
    [InlineData(true, true, true, 7)]
    public void GangStatusCombinesIdleEnemyAndIncomingStates(
        bool hasIdleGang,
        bool hasDetectedEnemyGang,
        bool hasPendingHire,
        int expectedState)
    {
        Assert.Equal(OriginalSpriteLayout.GangStatus(expectedState),
            GangStatusMarkerPresentation.Source(
                hasIdleGang, hasDetectedEnemyGang, hasPendingHire));
    }

    [Fact]
    public void IncomingOnlyUsesTheNinthNativeFrame() =>
        Assert.Equal(new(492, 227, 20, 20), OriginalSpriteLayout.IncomingGangStatus);
}
