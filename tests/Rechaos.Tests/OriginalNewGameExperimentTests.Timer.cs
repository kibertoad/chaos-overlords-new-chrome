using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> TimerRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].Timers.Count > 0) data.Add(experiment, run);
        return data;
    }

    // RULE-TIMER-001, RULE-TIMER-002, RULE-TIMER-003: the probe chooses a planning time limit and
    // lets turns run out without a Done press. It records the limit the match entry stored, each
    // redraw of the bar with the elapsed milliseconds, width and warning slot, and the elapsed
    // milliseconds of the last time-limit test that let planning go on and of the one that ended
    // it. The rebuild stores the same limit for the choice, draws the same width and plays the same
    // warning for each elapsed time, and lets the turn go on and end at the same elapsed times. The
    // original's redraws came between five and seven presentation ticks apart, the clock's jitter
    // around the six ticks the rebuild waits. The rebuild's PlanningTimer is then driven through
    // the recorded redraws, six ticks apart at the original's times, and must redraw on exactly
    // those ticks with the original's width and warning and expire where the original did. The
    // expiry ends the turn as a Done press does, which the replay of every run checks.
    [Theory]
    [MemberData(nameof(TimerRuns))]
    public void ThePlanningClockMatchesTheOriginals(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var limit = PlanningTimerPolicy.LimitMilliseconds((PlanningTimeLimit)recorded.PlanningLimitChoice);
        var redrawInterval = (int)(PresentationClock.Period * PlanningTimerPolicy.RefreshCountdown).TotalMilliseconds;
        foreach (var timer in recorded.Timers)
        {
            Assert.Equal<int?>(timer.LimitMs, limit);
            foreach (var (elapsed, width, slot) in timer.Bars)
            {
                Assert.True(PlanningTimerPolicy.RawBarWidth(timer.LimitMs, elapsed) == width,
                    $"turn {timer.Turn}, {elapsed} ms: the original drew width {width}");
                Assert.True((PlanningTimerPolicy.WarningSoundSlot(timer.LimitMs - elapsed) ?? 0) == slot,
                    $"turn {timer.Turn}, {elapsed} ms: the original played slot {slot}");
            }
            Assert.Equal(0, timer.Bars[0].Elapsed);
            for (var bar = 2; bar < timer.Bars.Count; bar++)
            {
                var interval = timer.Bars[bar].Elapsed - timer.Bars[bar - 1].Elapsed;
                Assert.InRange(interval, redrawInterval - PresentationClock.PeriodMilliseconds,
                    redrawInterval + PresentationClock.PeriodMilliseconds);
            }
            Assert.False(PlanningTimerPolicy.Expired(timer.LimitMs, timer.LastUnexpiredMs));
            Assert.True(PlanningTimerPolicy.Expired(timer.LimitMs, timer.ExpiredMs));
            ReplayThroughPlanningTimer((PlanningTimeLimit)recorded.PlanningLimitChoice, timer);
        }
    }

    private static void ReplayThroughPlanningTimer(PlanningTimeLimit limit, RecordedTimer recorded)
    {
        var bars = recorded.Bars;
        var countdown = PlanningTimerPolicy.RefreshCountdown;
        // The countdown left when planning started, from the first redraw after the start.
        var firstWait = (int)Math.Clamp(
            Math.Round(bars[1].Elapsed / (double)PresentationClock.PeriodMilliseconds), 1, countdown);
        var start = TimeSpan.FromHours(1);
        const long startTick = 1000;
        var timer = new PlanningTimer();
        timer.Advance(start, startTick - (countdown - firstWait));
        timer.Advance(start, startTick);
        timer.Start(limit, start);
        Assert.Equal(Math.Clamp(bars[0].Width, 0, PlanningTimerPolicy.BarWidth), timer.VisibleBarWidth);

        var tick = startTick;
        var previous = 0;
        for (var bar = 1; bar < bars.Count; bar++)
        {
            var (elapsed, width, slot) = bars[bar];
            var ticks = bar == 1 ? firstWait : countdown;
            for (var step = 1; step < ticks; step++)
            {
                var between = previous + (elapsed - previous) * step / ticks;
                var held = timer.VisibleBarWidth;
                Assert.Equal(PlanningTimerSignal.None,
                    timer.Advance(start + TimeSpan.FromMilliseconds(between), tick + step));
                Assert.Equal(held, timer.VisibleBarWidth);
            }
            tick += ticks;
            var now = start + TimeSpan.FromMilliseconds(elapsed);
            var signal = timer.Advance(now, tick);
            Assert.True(Math.Clamp(width, 0, PlanningTimerPolicy.BarWidth) == timer.VisibleBarWidth,
                $"turn {recorded.Turn}, {elapsed} ms: the original drew width {width}");
            Assert.True(slot == signal switch
                {
                    PlanningTimerSignal.LongWarning => 7,
                    PlanningTimerSignal.FinalWarning => 8,
                    _ => 0,
                }, $"turn {recorded.Turn}, {elapsed} ms: the original played slot {slot}");
            Assert.False(timer.HasExpired(now));
            previous = elapsed;
        }

        // Both tests fall less than a redraw after the last one.
        var lastUnexpired = start + TimeSpan.FromMilliseconds(recorded.LastUnexpiredMs);
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(lastUnexpired, tick + 1));
        Assert.False(timer.HasExpired(lastUnexpired));
        var expired = start + TimeSpan.FromMilliseconds(recorded.ExpiredMs);
        Assert.Equal(PlanningTimerSignal.None, timer.Advance(expired, tick + 1));
        Assert.True(timer.HasExpired(expired));
    }
}
