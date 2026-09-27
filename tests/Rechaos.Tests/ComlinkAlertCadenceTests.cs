using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ComlinkAlertCadenceTests
{
    [Fact]
    public void UnreadAlertStartsImmediatelyAndRepeatsEveryTwentyFourPresentationTicks()
    {
        // SCR-UI-003, RULE-UI-008: 24 ticks of the 166 ms clock, 3984 ms.
        var cadence = new ComlinkAlertCadence();

        Assert.Equal(TimeSpan.FromMilliseconds(3984), ComlinkAlertCadence.RepeatInterval);
        Assert.True(cadence.Advance(hasUnread: true, presentationActive: true, TimeSpan.Zero));
        Assert.False(cadence.Advance(true, true, TimeSpan.FromMilliseconds(3983)));
        Assert.True(cadence.Advance(true, true, TimeSpan.FromMilliseconds(3984)));
        Assert.False(cadence.Advance(true, true, TimeSpan.FromMilliseconds(7967)));
        Assert.True(cadence.Advance(true, true, TimeSpan.FromMilliseconds(7968)));
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
