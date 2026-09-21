using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class BackgroundRedrawCadenceTests
{
    [Fact]
    public void FocusedWindowDrawsEveryFrame()
    {
        var cadence = new BackgroundRedrawCadence();

        Assert.True(cadence.ShouldRedraw(windowActive: true, TimeSpan.Zero));
        Assert.True(cadence.ShouldRedraw(true, TimeSpan.FromMilliseconds(16)));
        Assert.True(cadence.ShouldRedraw(true, TimeSpan.FromMilliseconds(32)));
    }

    [Fact]
    public void LosingFocusDrawsOnceThenEveryTwoHundredMilliseconds()
    {
        var cadence = new BackgroundRedrawCadence();

        Assert.True(cadence.ShouldRedraw(true, TimeSpan.Zero));
        Assert.True(cadence.ShouldRedraw(windowActive: false, TimeSpan.FromMilliseconds(16)));
        Assert.False(cadence.ShouldRedraw(false, TimeSpan.FromMilliseconds(32)));
        Assert.False(cadence.ShouldRedraw(false, TimeSpan.FromMilliseconds(215)));
        Assert.True(cadence.ShouldRedraw(false, TimeSpan.FromMilliseconds(216)));
        Assert.False(cadence.ShouldRedraw(false, TimeSpan.FromMilliseconds(400)));
        Assert.True(cadence.ShouldRedraw(false, TimeSpan.FromMilliseconds(416)));
    }

    [Fact]
    public void RegainingFocusRestoresFullRateAndRearmsTheNextBackgroundSpell()
    {
        var cadence = new BackgroundRedrawCadence();

        Assert.True(cadence.ShouldRedraw(false, TimeSpan.Zero));
        Assert.False(cadence.ShouldRedraw(false, TimeSpan.FromMilliseconds(100)));
        Assert.True(cadence.ShouldRedraw(true, TimeSpan.FromMilliseconds(101)));
        Assert.True(cadence.ShouldRedraw(true, TimeSpan.FromMilliseconds(117)));
        Assert.True(cadence.ShouldRedraw(false, TimeSpan.FromMilliseconds(133)));
        Assert.False(cadence.ShouldRedraw(false, TimeSpan.FromMilliseconds(300)));
    }
}
