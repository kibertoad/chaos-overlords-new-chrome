using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

// RULE-TIMER-001, RULE-TIMER-002, RULE-TIMER-003: the planning clock's arithmetic and cadence.
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
    [InlineData(1.002, 7)]
    [InlineData(1.0, 8)]
    [InlineData(0.001, 8)]
    [InlineData(0.0, null)]
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

    [Theory]
    [InlineData(30.000, 60)]
    [InlineData(29.701, 60)]
    [InlineData(29.700, 60)]
    [InlineData(29.400, 59)]
    [InlineData(0.300, 1)]
    [InlineData(0.001, 1)]
    [InlineData(0.000, 0)]
    public void VisibleBarQuantizesElapsedPercentBeforeSixtyPixelScale(
        double remainingSeconds,
        int expectedWidth) =>
        Assert.Equal(expectedWidth, PlanningTimerPolicy.VisibleBarWidth(
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(remainingSeconds)));

    [Fact]
    public void PlanningTimerBarUsesOriginalMainPanelAperture() =>
        Assert.Equal(new Microsoft.Xna.Framework.Rectangle(520, 336, 60, 3),
            PlanningTimerLayout.Bar);

    // RULE-TIMER-002: the turn is still running when the elapsed milliseconds equal the limit.
    [Theory]
    [InlineData(29999, false)]
    [InlineData(30000, false)]
    [InlineData(30001, true)]
    public void TheTurnExpiresOnceTheElapsedTimeExceedsTheLimit(int elapsed, bool expired) =>
        Assert.Equal(expired, PlanningTimerPolicy.Expired(30000, elapsed));

    // RULE-TIMER-003: the bar is drawn at the start and then on every sixth presentation tick,
    // keeping its width in between, and each redraw plays the warning of its remaining time.
    [Fact]
    public void TimerRedrawsOnEverySixthPresentationTickAndThenExpires()
    {
        var period = PresentationClock.Period;
        var timer = new PlanningTimer();
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.Zero));
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        Assert.Equal(PlanningTimerPolicy.BarWidth, timer.VisibleBarWidth);

        for (var tick = 1; tick < PlanningTimerPolicy.RefreshCountdown; tick++)
            Assert.Equal(PlanningTimerSignal.None, timer.Advance(period * tick));
        Assert.Equal(PlanningTimerPolicy.BarWidth, timer.VisibleBarWidth);
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(period * PlanningTimerPolicy.RefreshCountdown));
        // 996 ms of 30000 is 3 percent: 60 - 180 / 100.
        Assert.Equal(59, timer.VisibleBarWidth);

        var redraws = 1;
        PlanningTimerSignal signal;
        var tickCount = PlanningTimerPolicy.RefreshCountdown;
        var signals = new List<PlanningTimerSignal>();
        do
        {
            tickCount++;
            signal = timer.Advance(period * tickCount);
            if (tickCount % PlanningTimerPolicy.RefreshCountdown == 0)
            {
                redraws++;
                signals.Add(signal);
            }
        } while (signal != PlanningTimerSignal.Expired);

        // 30000 ms is 180.7 ticks: the turn ends at tick 181, before its redraw at tick 186.
        Assert.Equal(181, tickCount);
        Assert.Equal(30, redraws);
        Assert.Equal(19, signals.Count(entry => entry == PlanningTimerSignal.None));
        Assert.Equal(9, signals.Count(entry => entry == PlanningTimerSignal.LongWarning));
        Assert.Equal(1, signals.Count(entry => entry == PlanningTimerSignal.FinalWarning));
        Assert.False(timer.IsActive);
    }

    [Fact]
    public void DisabledTimerNeverStartsOrExpires()
    {
        var timer = new PlanningTimer();

        timer.Start(PlanningTimeLimit.None, TimeSpan.Zero);

        Assert.False(timer.IsActive);
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.FromDays(1)));
        Assert.Equal(PlanningTimerPolicy.BarWidth, timer.VisibleBarWidth);
    }

    // RULE-TIMER-003: the redraw countdown runs between turns and is not reset when a turn starts,
    // so the first redraw after the start comes as many ticks later as the countdown had left.
    [Fact]
    public void StartingATurnLeavesTheRedrawCountdownWhereItWas()
    {
        var period = PresentationClock.Period;
        var timer = new PlanningTimer();
        timer.Advance(TimeSpan.Zero);
        for (var tick = 1; tick <= 4; tick++) timer.Advance(period * tick);

        // Started on tick 4, the countdown has two ticks left: the redraws fall on ticks 6 and 12.
        // A countdown reset at the start would redraw on tick 10 instead, at 996 ms elapsed, which
        // is 3 percent and width 59.
        timer.Start(PlanningTimeLimit.ThirtySeconds, period * 4);
        for (var tick = 5; tick <= 11; tick++) timer.Advance(period * tick);
        Assert.Equal(PlanningTimerPolicy.BarWidth, timer.VisibleBarWidth);

        // Tick 12 is 1328 ms elapsed, 4 percent: 60 - 240 / 100.
        timer.Advance(period * 12);
        Assert.Equal(58, timer.VisibleBarWidth);
    }

    [Fact]
    public void PausedTimerPreservesItsElapsedTimeUntilResumed()
    {
        var timer = new PlanningTimer();
        timer.Advance(TimeSpan.Zero);
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        timer.Advance(TimeSpan.FromSeconds(10));
        // 10000 ms of 30000 is 33 percent: 60 - 1980 / 100.
        Assert.Equal(41, timer.VisibleBarWidth);

        timer.Pause(TimeSpan.FromSeconds(10));

        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.FromHours(1)));
        Assert.Equal(41, timer.VisibleBarWidth);

        timer.Resume(TimeSpan.FromHours(1));

        Assert.NotEqual(PlanningTimerSignal.Expired,
            timer.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(20)));
        Assert.Equal(PlanningTimerSignal.Expired,
            timer.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(20.001)));
    }
}
