using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ComlinkAlertCadenceTests
{
    [Theory]
    [InlineData(0, 24)]
    [InlineData(1, 23)]
    [InlineData(2, 22)]
    [InlineData(3, 21)]
    [InlineData(4, 20)]
    [InlineData(5, 19)]
    [InlineData(6, 18)]
    [InlineData(7, 17)]
    public void ArrivalPreservesEightStepBlinkPhase(int phase, int delay)
    {
        // FND-AUDIO-012, RULE-AUDIO-008: reset repeat counter, retain blink step.
        var cadence = new ComlinkAlertCadence();
        var start = PresentationClock.Period * phase + TimeSpan.FromMilliseconds(50);
        Assert.True(cadence.Advance(true, true, start));
        var repeat = PresentationClock.Period * (phase + delay);
        Assert.False(cadence.Advance(true, true, repeat - TimeSpan.FromTicks(1)));
        Assert.True(cadence.Advance(true, true, repeat));
        Assert.False(cadence.Advance(true, true, repeat + PresentationClock.Period * 23));
        Assert.True(cadence.Advance(true, true, repeat + PresentationClock.Period * 24));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PlanningEntryAndNewDeliveryResetRepeatWhileAlreadyUnread(bool planning)
    {
        // FND-AUDIO-012: both entry and a new message sound immediately and reset repeat.
        var cadence = new ComlinkAlertCadence();
        Assert.True(cadence.Advance(true, true, TimeSpan.Zero, deliverySequence: 1));
        Assert.True(cadence.Advance(true, true, PresentationClock.Period * 15,
            enteringPlanning: planning, deliverySequence: planning ? 1 : 2));
        Assert.False(cadence.Advance(true, true, PresentationClock.Period * 24,
            deliverySequence: planning ? 1 : 2));
        Assert.True(cadence.Advance(true, true, PresentationClock.Period * 32,
            deliverySequence: planning ? 1 : 2));
    }

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
