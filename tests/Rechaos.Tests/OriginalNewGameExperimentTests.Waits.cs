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

    // RULE-TIMER-004, FND-TIMER-002, FND-UI-017: the probe recorded the ticks of the original's
    // presentation clock and every call of its wait (EXP-UI-024). The clock ticks every 166 ms on
    // average; every wait passes 1 and returns at the first tick after it starts; and a city-cell
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
        var period = (ticks[^1] - ticks[0]) / (double)(ticks.Count - 1);
        Assert.InRange(period, 163, 169);

        foreach (var wait in waits)
        {
            Assert.Equal(1, wait.Ticks);
            var next = ticks.First(tick => tick >= wait.Started);
            Assert.InRange(wait.Returned - next, 0, DebuggerSlackMs);
        }

        Assert.Equal(0, waits.Count % 3);
        var flashes = waits.Chunk(3).ToArray();
        Assert.Equal(recorded.HireSteps.Count(step => step.Slot >= 0), flashes.Length);
        foreach (var flash in flashes)
        {
            // The three waits follow one another with only the copies between them.
            Assert.InRange(flash[1].Started - flash[0].Returned, 0, DebuggerSlackMs);
            Assert.InRange(flash[2].Started - flash[1].Returned, 0, DebuggerSlackMs);
            Assert.Equal(TickedPresentation.LitPattern(TickedPresentationKind.CityCellFlash).Count(), flash.Length);

            // The rebuild's clock ticks at multiples of 166 ms; start its flash as far past a tick
            // as the original's first wait started past one.
            var previous = ticks.Last(tick => tick < flash[0].Started);
            var phase = (int)Math.Min(flash[0].Started - previous, 165);
            var start = 166 * 10 + phase;
            var step = new TickedPresentation();
            step.Start(TickedPresentationKind.CityCellFlash, new Rectangle(6, 45, 54, 52), null, null,
                TimeSpan.FromMilliseconds(start));
            var end = start;
            while (!step.TryFinish(TimeSpan.FromMilliseconds(end), out _)) end++;
            Assert.InRange(flash[2].Returned - flash[0].Started - (end - start), 0, 2 * DebuggerSlackMs);
        }
    }
}
