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
    // RULE-TIMER-002: the clock reports expiry and leaves stopping to the planning loop.
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
            else
            {
                Assert.Equal(PlanningTimerSignal.None, signal);
            }
        } while (!timer.HasExpired(period * tickCount));

        // 30000 ms is 180.7 ticks: the turn ends at tick 181, before its redraw at tick 186.
        Assert.Equal(181, tickCount);
        Assert.Equal(30, redraws);
        Assert.Equal(19, signals.Count(entry => entry == PlanningTimerSignal.None));
        Assert.Equal(9, signals.Count(entry => entry == PlanningTimerSignal.LongWarning));
        Assert.Equal(1, signals.Count(entry => entry == PlanningTimerSignal.FinalWarning));
        Assert.True(timer.IsActive);
        // Tick 180 is 29880 ms elapsed, 99 percent: 60 - 5940 / 100.
        Assert.Equal(1, timer.VisibleBarWidth);

        timer.Stop();
        Assert.False(timer.IsActive);
        Assert.False(timer.HasExpired(period * tickCount));
        // RULE-TIMER-002: the bar is left as last drawn when planning ends.
        Assert.True(timer.ShowsBar);
        Assert.Equal(1, timer.VisibleBarWidth);
    }

    // RULE-TIMER-002, RULE-TIMER-003: past the limit the clock goes on redrawing until the planning
    // loop tests it, so a panel open at the limit shows the empty bar, with no warning since the
    // remaining time is not above 0.
    [Fact]
    public void AnExpiredTurnThatIsNotTestedKeepsDrawingTheEmptyBar()
    {
        var period = PresentationClock.Period;
        var timer = new PlanningTimer();
        timer.Advance(TimeSpan.Zero);
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        for (var tick = 1; tick <= 240; tick++)
        {
            var signal = timer.Advance(period * tick);
            if (tick * PresentationClock.PeriodMilliseconds > 30000)
                Assert.Equal(PlanningTimerSignal.None, signal);
        }

        Assert.True(timer.IsActive);
        Assert.True(timer.HasExpired(period * 240));
        // Tick 240 redraws at 39840 ms elapsed, 132 percent, a width below 1: the empty bar.
        Assert.Equal(0, timer.VisibleBarWidth);
    }

    // RULE-TIMER-002: an untimed start and leaving the match forget the bar; a timed start draws
    // it full.
    [Fact]
    public void ClearingAndUntimedStartsForgetTheBar()
    {
        var timer = new PlanningTimer();
        timer.Advance(TimeSpan.Zero);
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        timer.Advance(TimeSpan.FromSeconds(10));
        timer.Stop();
        Assert.True(timer.ShowsBar);
        Assert.Equal(41, timer.VisibleBarWidth);

        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.FromSeconds(20));
        Assert.True(timer.ShowsBar);
        Assert.Equal(PlanningTimerPolicy.BarWidth, timer.VisibleBarWidth);

        timer.Advance(TimeSpan.FromSeconds(30));
        timer.Clear();
        Assert.False(timer.IsActive);
        Assert.False(timer.ShowsBar);
        Assert.Equal(PlanningTimerPolicy.BarWidth, timer.VisibleBarWidth);

        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.FromSeconds(40));
        timer.Advance(TimeSpan.FromSeconds(50));
        timer.Start(PlanningTimeLimit.None, TimeSpan.FromSeconds(60));
        Assert.False(timer.ShowsBar);
    }

    [Fact]
    public void DisabledTimerNeverStartsOrExpires()
    {
        var timer = new PlanningTimer();

        timer.Start(PlanningTimeLimit.None, TimeSpan.Zero);

        Assert.False(timer.IsActive);
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.FromDays(1)));
        Assert.False(timer.HasExpired(TimeSpan.FromDays(1)));
        Assert.False(timer.ShowsBar);
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

    // DEV-TIMER-002 on: the game menu stops the elapsed time of a timed turn until it closes.
    [Fact]
    public void WithDevTimer002OnTheGameMenuStopsTheElapsedTime()
    {
        var timer = new PlanningTimer { StopsInGameMenu = true };
        timer.Advance(TimeSpan.Zero);
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        timer.Advance(TimeSpan.FromSeconds(10));
        // 10000 ms of 30000 is 33 percent: 60 - 1980 / 100.
        Assert.Equal(41, timer.VisibleBarWidth);

        timer.Pause(TimeSpan.FromSeconds(10));

        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.FromHours(1)));
        Assert.False(timer.HasExpired(TimeSpan.FromHours(1)));
        Assert.Equal(41, timer.VisibleBarWidth);

        timer.Resume(TimeSpan.FromHours(1));

        Assert.False(timer.HasExpired(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(20)));
        Assert.True(timer.HasExpired(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(20.001)));
    }

    // DEV-TIMER-002 switched on in Options while the game menu holds the clock: the elapsed
    // time stops from that moment.
    [Fact]
    public void SwitchingDevTimer002OnWhileTheMenuIsOpenStopsTheElapsedTimeThere()
    {
        var timer = new PlanningTimer();
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        timer.Pause(TimeSpan.FromSeconds(10));

        timer.SetStopsInGameMenu(true, TimeSpan.FromSeconds(15));

        Assert.False(timer.HasExpired(TimeSpan.FromHours(1)));
        timer.Resume(TimeSpan.FromHours(1));
        Assert.False(timer.HasExpired(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(15)));
        Assert.True(timer.HasExpired(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(15.001)));
    }

    // DEV-TIMER-002 switched off in Options while the game menu holds the clock: the elapsed
    // time runs on from that moment.
    [Fact]
    public void SwitchingDevTimer002OffWhileTheMenuIsOpenRunsTheElapsedTimeFromThere()
    {
        var timer = new PlanningTimer { StopsInGameMenu = true };
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        timer.Pause(TimeSpan.FromSeconds(10));

        timer.SetStopsInGameMenu(false, TimeSpan.FromSeconds(100));

        Assert.False(timer.HasExpired(TimeSpan.FromSeconds(120)));
        Assert.True(timer.HasExpired(TimeSpan.FromSeconds(120.001)));
    }

    // RULE-TIMER-002, EXP-TURN-102: the original's menu bar leaves the elapsed time running, so a
    // turn can pass its limit in the menu; no bar is drawn while it is open.
    [Fact]
    public void TheElapsedTimeRunsOnInTheGameMenu()
    {
        var period = PresentationClock.Period;
        var timer = new PlanningTimer();
        timer.Advance(TimeSpan.Zero, 0);
        timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        timer.Advance(TimeSpan.FromSeconds(10), 60);
        Assert.Equal(41, timer.VisibleBarWidth);

        timer.Pause(TimeSpan.FromSeconds(10));
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(TimeSpan.FromSeconds(29), 174));
        Assert.Equal(41, timer.VisibleBarWidth);
        Assert.True(timer.HasExpired(TimeSpan.FromSeconds(31)));

        timer.Resume(TimeSpan.FromSeconds(40), PresentationClock.Ticks(period * 241));
        Assert.True(timer.HasExpired(TimeSpan.FromSeconds(40)));
    }

    // RULE-TIMER-003, EXP-TURN-102: the menu bar keeps the event pump from running, and the timer
    // flag keeps one tick for the first pass after it closes. With five ticks of the countdown left
    // when the menu opens, the first redraw after the close comes on the fourth tick, however long
    // the menu was open, timed or not.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheGameMenuKeepsOneTickOfTheRedrawCountdown(bool timed)
    {
        var period = PresentationClock.Period;
        var timer = new PlanningTimer();
        timer.Advance(TimeSpan.Zero, 0);
        if (timed) timer.Start(PlanningTimeLimit.ThirtySeconds, TimeSpan.Zero);
        timer.Advance(period, 1);

        timer.Pause(period * 1.5);
        for (var tick = 2; tick <= 72; tick++)
            Assert.Equal(PlanningTimerSignal.None, timer.Advance(period * tick, tick));
        timer.Resume(period * 72.5, 72);
        if (!timed) timer.Start(PlanningTimeLimit.ThirtySeconds, period * 72.5);
        var drawn = timer.VisibleBarWidth;

        for (var tick = 73; tick <= 75; tick++)
        {
            timer.Advance(period * tick, tick);
            Assert.Equal(drawn, timer.VisibleBarWidth);
            Assert.Equal(76 - tick, timer.RedrawCountdown);
        }
        timer.Advance(period * 76, 76);
        Assert.Equal(PlanningTimerPolicy.RefreshCountdown, timer.RedrawCountdown);
        var elapsed = PlanningTimerPolicy.WholeMilliseconds(period * (timed ? 76 : 3.5));
        Assert.Equal(PlanningTimerPolicy.VisibleBarWidth(30000, elapsed), timer.VisibleBarWidth);
    }
}
