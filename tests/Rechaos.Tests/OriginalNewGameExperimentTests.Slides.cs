using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> SlideRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].Slides is { Count: > 0 })
                    data.Add(experiment, run);
        return data;
    }

    // RULE-UI-003, FND-UI-011: the probe recorded each copy of the original's slide-ins
    // (EXP-UI-025), the Hire panel in the alternate form and the Move panel in the primary form. The
    // rebuild's step and copy sequence for the benchmark count the original read give the same
    // offsets. DEV-TIMER-001: the rebuild paces the copies at its fixed 84 a second, which also
    // takes the 16-pixel step, and its Hire screen and order panels show the same copies in order.
    [Theory]
    [MemberData(nameof(SlideRuns))]
    public void PanelsSlideInWithTheOriginalsCopies(string experiment, int run)
    {
        foreach (var slide in Run(experiment, run).Slides!)
        {
            Assert.Equal(PanelSlideTransition.SlideInOffsets(slide.Travel,
                PanelSlideTransition.SlideStep(slide.Travel, slide.Benchmark), slidePanels: true), slide.Offsets);

            var transition = new PanelSlideTransition();
            var start = TimeSpan.FromSeconds(10);
            ClientScreen screen;
            if (slide.Travel == PanelSlideTransition.AlternateStartOffset)
            {
                screen = ClientScreen.Hire;
                transition.Begin(ClientScreen.City, screen, start);
            }
            else
            {
                Assert.Equal(PanelSlideTransition.StartOffset, slide.Travel);
                screen = ClientScreen.Commands;
                transition.BeginOrderPanel(ClientScreen.Sector, start);
            }
            var shown = new List<int>();
            for (var copy = 0; copy <= slide.Offsets.Count; copy++)
            {
                var offset = transition.Offset(screen, start + TimeSpan.FromTicks(
                    copy * TimeSpan.TicksPerSecond / PanelSlideTransition.NominalBlitBenchmarkCount + 1));
                if (shown.Count == 0 || shown[^1] != offset) shown.Add(offset);
                if (offset == 0) break;
            }
            Assert.Equal(slide.Offsets, shown);
        }
    }
}
