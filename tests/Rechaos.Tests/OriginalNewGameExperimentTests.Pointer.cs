using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> PointerRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].PointerCalls is not null)
                    data.Add(experiment, run);
        return data;
    }

    // RULE-UI-007, FND-UI-034: the probe recorded every call of the original's cursor helper
    // (EXP-UI-022). The original passes only the arrow and the hourglass, always forced, makes every
    // roll from the setup's hourglass on under the hourglass, and takes the human's planning under
    // the arrow: the last call before each Done press is the arrow and the first after it the
    // hourglass. Between the press and the next planning entry each arrow is followed by the next
    // hourglass with no roll between, so the player sees the hourglass for the whole stretch. The
    // rebuild plans the computers an update after the press, and between those updates it shows
    // the idle shape of the replay's state: the hourglass after the press, the arrow at the entry.
    [Theory]
    [MemberData(nameof(PointerRuns))]
    public void TheHourglassCoversTheWorkBetweenThePlanningEntries(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var calls = recorded.PointerCalls!;
        Assert.All(calls, call =>
        {
            Assert.True(call.Shape is (int)PointerShape.Arrow or (int)PointerShape.Hourglass, $"shape {call.Shape}");
            Assert.Equal(1, call.Force);
        });
        // A roll is made under the last shape selected before it.
        var first = calls.First(call => call.Shape == (int)PointerShape.Hourglass).AfterRoll;
        for (var roll = first; roll < recorded.Rolls.Count; roll++)
            Assert.Equal((int)PointerShape.Hourglass, calls.Last(call => call.AfterRoll <= roll).Shape);
        for (var done = 1; done <= recorded.DoneCount; done++)
        {
            Assert.Equal((int)PointerShape.Arrow, calls.Last(call => call.Done < done).Shape);
            var stretch = calls.Where(call => call.Done == done).ToArray();
            Assert.NotEmpty(stretch);
            Assert.Equal((int)PointerShape.Hourglass, stretch[0].Shape);
            // Every arrow but the planning entry's is followed by an hourglass at the same roll.
            for (var index = 0; index < stretch.Length - 1; index++)
                if (stretch[index].Shape == (int)PointerShape.Arrow)
                {
                    Assert.Equal((int)PointerShape.Hourglass, stretch[index + 1].Shape);
                    Assert.Equal(stretch[index].AfterRoll, stretch[index + 1].AfterRoll);
                }
        }
        // The stretch after the last press ends at the arrow too, which no later press checks.
        Assert.Equal((int)PointerShape.Arrow, calls[^1].Shape);

        var atEntries = new List<PointerShape>();
        var afterPresses = new List<PointerShape>();
        StartMatch(recorded, out var presses,
            atPlanningEntry: (state, _, _) => atEntries.Add(PresentationPointer.Idle(state)),
            afterDone: state => afterPresses.Add(PresentationPointer.Idle(state)));
        Assert.Equal(recorded.DoneCount, presses);
        Assert.All(atEntries, shape => Assert.Equal(PointerShape.Arrow, shape));
        Assert.All(afterPresses, shape => Assert.Equal(PointerShape.Hourglass, shape));
    }
}
