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
    // EXP-TURN-102 holds the menu bar open in each timed turn: no bar is drawn while it is open, the
    // countdown takes one tick for the whole hold, and the elapsed time runs on, so a turn held
    // past its limit ends at the first test after the close. Its turns are replayed tick by tick
    // through the recorded ticks of timer slot 0, with the clock paused for the hold.
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
            // --clock-captures copies the drawing area after the start helper has read the time
            // and before it draws the bar, so the first bar of those runs comes a few
            // milliseconds into the turn.
            Assert.InRange(timer.Bars[0].Elapsed, 0, recorded.ClockCaptures.Count > 0 ? ClockCaptureDelayMs : 0);
            var menu = recorded.Menus.SingleOrDefault(hold => hold.Turn == timer.Turn);
            for (var bar = 2; bar < timer.Bars.Count; bar++)
            {
                if (menu is not null && timer.Bars[bar - 1].Elapsed < menu.PostedMs && timer.Bars[bar].Elapsed > menu.ClosedMs)
                    continue;
                var interval = timer.Bars[bar].Elapsed - timer.Bars[bar - 1].Elapsed;
                Assert.InRange(interval, redrawInterval - PresentationClock.PeriodMilliseconds,
                    redrawInterval + PresentationClock.PeriodMilliseconds);
            }
            Assert.False(PlanningTimerPolicy.Expired(timer.LimitMs, timer.LastUnexpiredMs));
            Assert.True(PlanningTimerPolicy.Expired(timer.LimitMs, timer.ExpiredMs));
            if (menu is null) ReplayThroughPlanningTimer((PlanningTimeLimit)recorded.PlanningLimitChoice, timer);
            else ReplayThroughMenu((PlanningTimeLimit)recorded.PlanningLimitChoice, timer, menu);
        }
    }

    public static TheoryData<string, int> ClockCaptureRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].ClockCaptures.Count > 0) data.Add(experiment, run);
        return data;
    }

    // RULE-TIMER-002: at every start of a human's clock, before the start draws the bar, the bar's
    // rectangle holds what an untimed planning entry shows there, the console's own art (the
    // endpoint of EXP-UI-001, which ScreenCaptureTests compares with the rebuild), whatever bar the
    // turn before left: a part-drawn one after a Done, the empty one after a turn that ran out,
    // another human's in hot seat. The planning entry redraws the console, and the rebuild forgets
    // the bar there (PlanningTimerLoopTests).
    [Theory]
    [MemberData(nameof(ClockCaptureRuns))]
    public void EveryPlanningEntryPutsBackTheConsolesBar(string experiment, int run)
    {
        var console = ScreenCaptureRecord.LoadAll()
            .Single(capture => capture.Experiment == "EXP-UI-001" && capture.Run == 0 && capture.Step == -1)
            .Elements.Single(element => element.Element == "Planning clock bar");
        var captures = Run(experiment, run).ClockCaptures;
        Assert.Contains(captures, capture => capture.LastWidth is >= 0 and < PlanningTimerPolicy.BarWidth);
        Assert.All(captures, capture => Assert.Equal(console.Xxh3, capture.Xxh3));
    }

    // The longest a --clock-captures copy has held up the first bar of a turn.
    private const int ClockCaptureDelayMs = 20;

    // RULE-TIMER-003: replays a turn with the menu bar held open through the recorded ticks of timer
    // slot 0. A tick is the pump's when no bar was drawn before it and the next one; a bar the
    // original drew between a tick and the next is the redraw of that tick, drawn at the bar's
    // elapsed time. The clock pauses from the posting of the opening key to the close, which keeps
    // one tick for the first pass after it, and the turn has to redraw on exactly the original's
    // ticks, draw nothing while the menu is open, and expire where the original did.
    private static void ReplayThroughMenu(PlanningTimeLimit limit, RecordedTimer recorded, RecordedMenu menu)
    {
        var bars = recorded.Bars;
        var ticks = menu.Ticks;
        Assert.DoesNotContain(bars, bar => bar.Elapsed > menu.PostedMs && bar.Elapsed < menu.ClosedMs);
        // The countdown left at the start, from the ticks up to the first redraw after it.
        var firstWait = ticks.Count(tick => tick < bars[1].Elapsed);
        Assert.InRange(firstWait, 1, PlanningTimerPolicy.RefreshCountdown);
        var start = TimeSpan.FromHours(1);
        const long startTick = 1000;
        var timer = new PlanningTimer();
        timer.Advance(start, startTick - (PlanningTimerPolicy.RefreshCountdown - firstWait));
        timer.Advance(start, startTick);
        timer.Start(limit, start);

        TimeSpan At(int elapsed) => start + TimeSpan.FromMilliseconds(elapsed);
        var paused = false;
        var drawn = 1;
        for (var index = 0; index < ticks.Count; index++)
        {
            var tick = startTick + index + 1;
            if (!paused && ticks[index] > menu.PostedMs && ticks[index] < menu.ClosedMs)
            {
                timer.Pause(At(menu.PostedMs));
                paused = true;
            }
            if (paused && ticks[index] > menu.ClosedMs)
            {
                timer.Resume(At(menu.ClosedMs), tick - 1);
                paused = false;
            }
            var next = index + 1 < ticks.Count ? ticks[index + 1] : int.MaxValue;
            var bar = bars.Skip(drawn).Where(bar => bar.Elapsed >= ticks[index] && bar.Elapsed < next)
                .Select(bar => ((int Elapsed, int Width, int Slot)?)bar).FirstOrDefault();
            var held = timer.VisibleBarWidth;
            var signal = timer.Advance(At(bar?.Elapsed ?? ticks[index]), tick);
            if (bar is not { } redraw)
            {
                Assert.True(PlanningTimerSignal.None == signal && held == timer.VisibleBarWidth,
                    $"turn {recorded.Turn}, tick at {ticks[index]} ms: the rebuild redrew, the original did not");
                continue;
            }
            drawn++;
            Assert.True(Math.Clamp(redraw.Width, 0, PlanningTimerPolicy.BarWidth) == timer.VisibleBarWidth,
                $"turn {recorded.Turn}, {redraw.Elapsed} ms: the original drew width {redraw.Width}, the rebuild {timer.VisibleBarWidth}");
            Assert.True(redraw.Slot == signal switch
                {
                    PlanningTimerSignal.LongWarning => 7,
                    PlanningTimerSignal.FinalWarning => 8,
                    _ => 0,
                }, $"turn {recorded.Turn}, {redraw.Elapsed} ms: the original played slot {redraw.Slot}");
        }
        Assert.Equal(bars.Count, drawn);

        // A hold that outlasted the turn's last tick closes after it.
        if (paused) timer.Resume(At(menu.ClosedMs), startTick + ticks.Count);
        Assert.False(timer.HasExpired(At(recorded.LastUnexpiredMs)));
        Assert.True(timer.HasExpired(At(recorded.ExpiredMs)));
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
