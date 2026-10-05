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
    // turn-start cue at the turns that began after them (EXP-AUDIO-001).
    [Theory]
    [MemberData(nameof(SoundRuns))]
    public void ANewGameHasNoTurnStartSound(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var calls = recorded.SoundCalls!;
        // Begin and each Done press played the push cue at the press, so the recording reached the
        // helper. The push cue goes through the effects wrapper, so this holds only for a run made
        // with --sound; the fixture does not record that, and every run with sound_calls so far was.
        // A turn left to the planning time limit has no press and so no cue.
        Assert.Contains(calls, call => call.Done == 0 && call.Slot == AudioRouting.PointerPushSound());
        for (var done = 1; done <= recorded.DoneCount; done++)
            if (!recorded.ExpiredTurns.Contains(done))
                Assert.Contains(calls, call => call.Done == done && call.AfterRoll == recorded.DoneAtRoll[done - 1]
                    && call.Slot == AudioRouting.PointerPushSound());
        Assert.DoesNotContain(calls, call => call.Slot == GeneralSoundSlot.TurnStartCue);

        // The rebuild replays the recorded turns. Its only call of the turn-start cue is in the
        // handler of an online match's resolved turn (ChaosGame.MultiplayerNotices), which a local
        // game never reaches, and the routing gives a local game no cue. The replay does not
        // observe the rebuild's sounds.
        StartMatch(recorded, out var presses);
        Assert.Equal(recorded.DoneCount, presses);
        Assert.Null(AudioRouting.TurnStartSound(networkGame: false));
    }
}
