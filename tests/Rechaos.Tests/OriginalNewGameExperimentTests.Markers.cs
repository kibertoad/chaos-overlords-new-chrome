using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> GangMarkerRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].GangMarkers.Count > 0 && !KnownDivergences.ContainsKey((experiment, run)))
                    data.Add(experiment, run);
        return data;
    }

    // RULE-UI-006, FND-UI-024: the probe logged every gang-status marker the original drew from the
    // last full city redraw before the dump on, through the hire steps and the presses after it.
    // The map the log leaves after each step is the one the rebuild's marker map shows once it has
    // taken the same steps: hire steps through the dock's orders, and a Search panel opened from the
    // console and closed with Done, which draws the whole map again.
    [Theory]
    [MemberData(nameof(GangMarkerRuns))]
    public void TheCityKeepsTheOriginalsGangMarkers(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = StartMatch(recorded, out _);
        var human = recorded.Humans[0];
        var sight = GangSightSnapshot.Capture(match, human);
        var map = new GangStatusMarkerMap();
        var rows = SiteSearchPanel.Rows(match.Definitions);
        var searchOpen = false;
        var steps = recorded.HireSteps.Count + recorded.OrderSteps.Count;
        for (var step = 0; step <= steps; step++)
        {
            var label = "at the dump";
            if (step > 0 && step <= recorded.HireSteps.Count)
            {
                var hire = recorded.HireSteps[step - 1];
                TakeHireStep(match, human, hire);
                label = $"after hire step {step}";
            }
            else if (step > 0)
            {
                var press = recorded.OrderSteps[step - recorded.HireSteps.Count - 1];
                Assert.True(press.Kind == "strip" && press.Menu == -1, "only plain presses are replayed here");
                var point = new Point(press.X, press.Y);
                if (!searchOpen) searchOpen = CityConsoleLayout.ActionAt(point) == CityConsoleAction.Search;
                else if (SiteSearchPanel.HitTest(point, rows.Length).Control == SiteSearchControl.Done)
                {
                    searchOpen = false;
                    map.RedrawAll(match, human, sight);
                }
                label = $"after the press at ({press.X}, {press.Y})";
            }
            Assert.True(OriginalMarkers(recorded, step).SequenceEqual(map.Frames(match, human, sight)),
                $"{label}: the original shows {Describe(OriginalMarkers(recorded, step))}, the rebuild {Describe(map.Frames(match, human, sight))}");
        }
    }

    // The map the original's drawings leave by the end of a step: a full redraw starts from a map
    // without markers, a frame or the incoming-only mark lands on its sector, and the copy back
    // clears one. A sector of -1 lies off the map.
    private static int[] OriginalMarkers(RecordedRun recorded, int step)
    {
        var frames = Enumerable.Repeat(-1, MatchLimits.SectorCount).ToArray();
        foreach (var draw in recorded.GangMarkers.Where(draw => draw[0] <= step))
        {
            var sector = draw[3];
            if (draw[1] == 0) Array.Fill(frames, -1);
            else if (sector >= 0) frames[sector] = draw[1] == 2 ? -1 : draw[4];
        }
        return frames;
    }

    private static string Describe(int[] frames) => string.Join(", ",
        frames.Select((frame, sector) => (frame, sector)).Where(entry => entry.frame >= 0)
            .Select(entry => $"{entry.sector}:{entry.frame}"));
}
