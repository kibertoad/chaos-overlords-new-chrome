using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> SoundRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].SoundCalls is not null)
                    data.Add(experiment, run);
        return data;
    }

    // RULE-AUDIO-006: the probe recorded every call of the original's play helper, which the
    // turn-start cue calls directly whatever the effects setting (FND-AUDIO-006). In a game started
    // with New Game the helper played the push cue for Begin and for each Done press, and never the
    // turn-start cue at the turns that began after them (EXP-AUDIO-001). The rebuild's local game
    // has no turn-start cue either.
    [Theory]
    [MemberData(nameof(SoundRuns))]
    public void ANewGameHasNoTurnStartSound(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var calls = recorded.SoundCalls!;
        // Each Done press played the push cue, so the recording reached the helper.
        for (var done = 1; done <= recorded.DoneCount; done++)
            Assert.Contains(calls, call => call.Done == done && call.Slot == AudioRouting.PointerPushSound());
        Assert.DoesNotContain(calls, call => call.Slot == GeneralSoundSlot.TurnStartCue);

        StartMatch(recorded, out var presses);
        Assert.Equal(recorded.DoneCount, presses);
        Assert.Null(AudioRouting.TurnStartSound(networkGame: false));
    }
}
