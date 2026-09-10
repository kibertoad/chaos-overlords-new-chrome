using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class PlanningTimerPolicyTests
{
    [Theory]
    [InlineData(PlanningTimeLimit.None, null)]
    [InlineData(PlanningTimeLimit.ThirtySeconds, 30)]
    [InlineData(PlanningTimeLimit.TwoMinutes, 120)]
    [InlineData(PlanningTimeLimit.FiveMinutes, 300)]
    public void ChoicesUseRecoveredOriginalDurations(
        PlanningTimeLimit limit, int? seconds)
    {
        var duration = PlanningTimerPolicy.Duration(limit);

        Assert.Equal(seconds, duration is null ? null : (int)duration.Value.TotalSeconds);
    }

    [Theory]
    [InlineData(10.0, null)]
    [InlineData(9.999, 7)]
    [InlineData(1.001, 7)]
    [InlineData(1.0, 8)]
    [InlineData(0.0, 8)]
    [InlineData(-0.001, null)]
    public void WarningSlotsUseRecoveredStrictCountdownBoundaries(
        double remainingSeconds, int? expectedSlot) =>
        Assert.Equal(expectedSlot, PlanningTimerPolicy.WarningSoundSlot(
            TimeSpan.FromSeconds(remainingSeconds)));

    [Theory]
    [InlineData(30, 60)]
    [InlineData(15, 30)]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(31, 60)]
    public void VisibleBarUsesRecoveredSixtyPixelScale(int remainingSeconds, int width) =>
        Assert.Equal(width, PlanningTimerPolicy.VisibleBarWidth(
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(remainingSeconds)));

    [Fact]
    public void PlanningTimerBarUsesOriginalMainPanelAperture() =>
        Assert.Equal(new Microsoft.Xna.Framework.Rectangle(520, 336, 60, 3),
            PlanningTimerLayout.Bar);

    [Fact]
    public void TimerEmitsEachWarningBucketOnceAndThenExpires()
    {
        var timer = new PlanningTimer();
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);

        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.FromSeconds(20)));
        Assert.Equal(PlanningTimerSignal.LongWarning, timer.Advance(TimeSpan.FromSeconds(20.001)));
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.FromSeconds(20.1)));
        Assert.Equal(PlanningTimerSignal.LongWarning, timer.Advance(TimeSpan.FromSeconds(21.001)));
        Assert.Equal(PlanningTimerSignal.FinalWarning, timer.Advance(TimeSpan.FromSeconds(29.001)));
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.FromSeconds(29.1)));
        Assert.Equal(PlanningTimerSignal.Expired, timer.Advance(TimeSpan.FromSeconds(30)));
        Assert.False(timer.IsActive);
    }

    [Fact]
    public void DisabledTimerNeverStartsOrExpires()
    {
        var timer = new PlanningTimer();

        timer.Start(PlanningTimeLimit.None, TimeSpan.Zero);

        Assert.False(timer.IsActive);
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.FromDays(1)));
        Assert.Equal(PlanningTimerPolicy.BarWidth, timer.VisibleBarWidth(TimeSpan.FromDays(1)));
    }
}
