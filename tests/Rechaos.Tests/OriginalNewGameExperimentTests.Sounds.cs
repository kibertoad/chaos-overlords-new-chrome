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
    // with New Game the helper played the push cue for Begin and for each Done press when effects
    // were enabled, and never the turn-start cue at the turns that began after them (EXP-AUDIO-001).
    [Theory]
    [MemberData(nameof(SoundRuns))]
    public void ANewGameHasNoTurnStartSound(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var calls = recorded.SoundCalls!;
        // The push cue goes through the effects wrapper, which calls the helper only while
        // effects_enabled is set (FND-AUDIO-002), so which calls a run can hold depends on it, and
        // a run that does not record it cannot be read.
        Assert.True(recorded.EffectsEnabled.HasValue,
            $"{experiment} run {run} records sound_calls without effects_enabled; extract it again from a run that records it.");
        AssertSoundCalls(calls, recorded.EffectsEnabled!.Value, recorded.DoneAtRoll, recorded.ExpiredTurns);

        // The rebuild replays the recorded turns. Its only call of the turn-start cue is in the
        // handler of an online match's resolved turn (ChaosGame.MultiplayerNotices), which a local
        // game never reaches, and the routing gives a local game no cue. The replay does not
        // observe the rebuild's sounds.
        Assert.Equal(recorded.DoneCount, Replayed(recorded).DonePresses);
        Assert.Null(AudioRouting.TurnStartSound(networkGame: false));
    }

    // The calls a run with this effects setting may hold (RULE-AUDIO-006, EXP-AUDIO-001).
    private static void AssertSoundCalls(IReadOnlyList<RecordedSoundCall> calls, bool effectsEnabled,
        IReadOnlyList<int> doneAtRoll, IReadOnlySet<int> expiredTurns)
    {
        if (effectsEnabled)
        {
            // Begin and each Done press played the push cue at the press, so the recording reached
            // the helper. A turn left to the planning time limit has no press and so no cue.
            Assert.Contains(calls, call => call.Done == 0 && call.Slot == AudioRouting.PointerPushSound());
            for (var done = 1; done <= doneAtRoll.Count; done++)
                if (!expiredTurns.Contains(done))
                    Assert.Contains(calls, call => call.Done == done && call.AfterRoll == doneAtRoll[done - 1]
                        && call.Slot == AudioRouting.PointerPushSound());
        }
        else
        {
            // With effects off the wrapper calls nothing, so no press reaches the helper with the
            // push cue; only a direct call, such as the turn-start cue's (BUG-AUDIO-001), can be
            // recorded, and the check below finds none of those. No press confirms that the
            // breakpoint was armed, so an empty list passes as well.
            Assert.DoesNotContain(calls, call => call.Slot == AudioRouting.PointerPushSound());
        }
        Assert.DoesNotContain(calls, call => call.Slot == GeneralSoundSlot.TurnStartCue);
    }

    // No recorded run has effects off, so the effects-off expectation is checked on made-up calls:
    // a push cue or the turn-start cue is refused there, a call of another slot is accepted, and
    // an effects-on run without the push cue is refused.
    [Fact]
    public void AnEffectsOffRunRefusesThePushCue()
    {
        int[] doneAtRoll = [10, 20];
        var expired = new HashSet<int>();
        var pushCue = new RecordedSoundCall(10, 1, AudioRouting.PointerPushSound(), 0);
        var other = new RecordedSoundCall(5, 0, GeneralSoundSlot.PanelOpen, 0);
        var turnStart = new RecordedSoundCall(10, 1, GeneralSoundSlot.TurnStartCue, 0);

        AssertSoundCalls([], effectsEnabled: false, doneAtRoll, expired);
        AssertSoundCalls([other], effectsEnabled: false, doneAtRoll, expired);
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertSoundCalls([other, pushCue], effectsEnabled: false, doneAtRoll, expired));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertSoundCalls([turnStart], effectsEnabled: false, doneAtRoll, expired));
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() =>
            AssertSoundCalls([other], effectsEnabled: true, doneAtRoll, expired));
    }
}
