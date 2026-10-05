using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> WaitRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].Waits is { Count: > 0 })
                    data.Add(experiment, run);
        return data;
    }

    // Under the debugger a tick's callback and a breakpoint's report each come a little late, so the
    // original's times are compared within this margin.
    private const int DebuggerSlackMs = 30;

    // FND-UI-017: the city-cell flash fn_0041ACE6 spans 0x0041ACE6..0x0041B4E9, so a wait called
    // from inside that range is one of a flash's.
    private static bool CalledByCityCellFlash(RecordedWait wait) =>
        wait.Call is >= 0x0041ACE6 and < 0x0041B4E9;

    // RULE-TIMER-004, FND-TIMER-002, FND-UI-017: the probe recorded the ticks of the original's
    // presentation clock and every call of its wait (EXP-UI-024). The clock ticks about every
    // 166 ms; every wait passes 1 and returns at the first tick after it starts; and a city-cell
    // flash, the Hire handler's for the sector a portrait is dropped on, makes three waits back to
    // back. The rebuild's flash, started at the same point of its own 166 ms clock, ends with the
    // original's third wait.
    [Theory]
    [MemberData(nameof(WaitRuns))]
    public void PresentationWaitsEndAtTheNextTickOfTheClock(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var ticks = recorded.Ticks!;
        var waits = recorded.Waits!;
        var periodMs = PresentationClock.PeriodMilliseconds;
        var period = (ticks[^1] - ticks[0]) / (double)(ticks.Count - 1);
        Assert.InRange(period, periodMs - 3, periodMs + 3);

        foreach (var wait in waits)
        {
            Assert.Equal(1, wait.Ticks);
            // The probe writes -1 for a wait that had not returned when the run ended, and a wait
            // after the last recorded tick has no tick to end at: both mean an incomplete recording.
            Assert.True(wait.Returned >= 0, $"The wait called at 0x{wait.Call:X8} had not returned when the run ended.");
            Assert.True(wait.Started <= ticks[^1], $"The wait called at 0x{wait.Call:X8} started after the last recorded tick.");
            var next = ticks.First(tick => tick >= wait.Started);
            Assert.InRange(wait.Returned - next, 0, DebuggerSlackMs);
        }

        // A drop on a sector flashes its cell; a Reject press or an Exit does not.
        var waitsPerFlash = TickedPresentation.LitPattern(TickedPresentationKind.CityCellFlash).Count;
        var flashWaits = waits.Where(CalledByCityCellFlash).ToArray();
        Assert.Equal(0, flashWaits.Length % waitsPerFlash);
        var flashes = flashWaits.Chunk(waitsPerFlash).ToArray();
        Assert.Equal(recorded.HireSteps.Count(step => step.Sector >= 0), flashes.Length);
        foreach (var flash in flashes)
        {
            // The waits follow one another with only the copies between them.
            for (var i = 1; i < flash.Length; i++)
                Assert.InRange(flash[i].Started - flash[i - 1].Returned, 0, DebuggerSlackMs);

            // The rebuild's clock ticks at multiples of its period; start its flash as far past a
            // tick as the original's first wait started past one.
            Assert.True(flash[0].Started > ticks[0], "A flash started before the first recorded tick, so its phase is unknown.");
            var previous = ticks.Last(tick => tick < flash[0].Started);
            var phase = (int)Math.Min(flash[0].Started - previous, periodMs - 1);
            var start = periodMs * 10 + phase;
            var step = new TickedPresentation();
            step.Start(TickedPresentationKind.CityCellFlash, new Rectangle(6, 45, 54, 52), null, null,
                TimeSpan.FromMilliseconds(start));
            var end = start;
            while (!step.TryFinish(TimeSpan.FromMilliseconds(end), out _)) end++;
            Assert.InRange(flash[^1].Returned - flash[0].Started - (end - start), 0, 2 * DebuggerSlackMs);
        }
    }
}
