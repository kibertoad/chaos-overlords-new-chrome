using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ComlinkAlertCadenceTests
{
    [Fact]
    public void UnreadAlertStartsImmediatelyAndRepeatsEveryFourSeconds()
    {
        var cadence = new ComlinkAlertCadence();

        Assert.True(cadence.Advance(hasUnread: true, presentationActive: true, TimeSpan.Zero));
        Assert.False(cadence.Advance(true, true, TimeSpan.FromSeconds(3.999)));
        Assert.True(cadence.Advance(true, true, TimeSpan.FromSeconds(4)));
        Assert.False(cadence.Advance(true, true, TimeSpan.FromSeconds(7.999)));
        Assert.True(cadence.Advance(true, true, TimeSpan.FromSeconds(8)));
    }

    [Fact]
    public void HandoffDefersInitialAlertUntilPlanningPresentationStarts()
    {
        var cadence = new ComlinkAlertCadence();

        Assert.False(cadence.Advance(true, presentationActive: false, TimeSpan.Zero));
        Assert.False(cadence.Advance(true, presentationActive: false, TimeSpan.FromMinutes(1)));
        Assert.True(cadence.Advance(true, presentationActive: true, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void ReadingInboxCancelsRepeatAndRearmsFutureUnreadDelivery()
    {
        var cadence = new ComlinkAlertCadence();

        Assert.True(cadence.Advance(true, true, TimeSpan.Zero));
        Assert.False(cadence.Advance(false, true, TimeSpan.FromSeconds(1)));
        Assert.False(cadence.Advance(false, true, TimeSpan.FromSeconds(8)));
        Assert.True(cadence.Advance(true, true, TimeSpan.FromSeconds(9)));
    }
}
