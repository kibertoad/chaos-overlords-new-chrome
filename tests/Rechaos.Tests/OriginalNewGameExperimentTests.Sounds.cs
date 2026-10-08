using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    // FND-AUDIO-019: the effects wrapper's call of the play helper, and the title initialization's
    // call of the level setup, which comes after the probe has written the levels.
    private const int WrapperHelperCall = 0x004642AB;
    private const int TitleLevelSetupCall = 0x00461088;

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
    // with New Game the helper never played the turn-start cue at the turns that began after Begin,
    // with effects enabled (EXP-AUDIO-001) or not (EXP-AUDIO-002).
    [Theory]
    [MemberData(nameof(SoundRuns))]
    public void ANewGameHasNoTurnStartSound(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        // The push cue goes through the effects wrapper, which calls the helper only while
        // effects_enabled is set (FND-AUDIO-002), so which calls a run can hold depends on it. A
        // run that does not record it, the wrapper's calls and the level setups cannot be read.
        Assert.True(recorded.EffectsEnabled.HasValue,
            $"{experiment} run {run} records sound_calls without effects_enabled; extract it again from a run that records it.");
        Assert.True(recorded.EffectCalls is not null && recorded.LevelSetups is not null,
            $"{experiment} run {run} records sound_calls without effect_calls or level_setups; record it again with the current probe.");
        AssertSoundCalls(recorded.SoundCalls!, recorded.EffectCalls!, recorded.LevelSetups!, recorded.EffectsEnabled!.Value,
            recorded.DoneAtRoll, recorded.ExpiredTurns);

        // The rebuild replays the recorded turns. Its only call of the turn-start cue is in the
        // handler of an online match's resolved turn (ChaosGame.MultiplayerNotices), which a local
        // game never reaches, and the routing gives a local game no cue. The replay does not
        // observe the rebuild's sounds.
        Assert.Equal(recorded.DoneCount, Replayed(recorded).DonePresses);
        Assert.Null(AudioRouting.TurnStartSound(networkGame: false));
    }

    // The calls a run with this effects setting may hold (RULE-AUDIO-006, EXP-AUDIO-001,
    // EXP-AUDIO-002), and the proof that the recording ran and that the setting held throughout.
    private static void AssertSoundCalls(IReadOnlyList<RecordedSoundCall> calls, IReadOnlyList<RecordedSoundCall> effectCalls,
        IReadOnlyList<RecordedLevelSetup> levelSetups, bool effectsEnabled, IReadOnlyList<int> doneAtRoll,
        IReadOnlySet<int> expiredTurns)
    {
        // The level setup is the only code that writes effects_enabled, and its first call, in the
        // title initialization, comes after the probe has written the levels (FND-AUDIO-019). Every
        // call left the setting the run reports, so it held from before Begin to the end of the run.
        Assert.NotEmpty(levelSetups);
        Assert.Equal(TitleLevelSetupCall, levelSetups[0].Call);
        Assert.Equal(0, levelSetups[0].Done);
        Assert.All(levelSetups, setup => Assert.Equal(effectsEnabled, setup.EffectsEnabled));

        // Begin and each Done press asked the wrapper for the push cue at the press, so the
        // recording ran whatever the setting. A turn left to the planning time limit has no press
        // and so no cue.
        var pushCue = AudioRouting.PointerPushSound();
        Assert.Contains(effectCalls, call => call.Done == 0 && call.Slot == pushCue);
        for (var done = 1; done <= doneAtRoll.Count; done++)
            if (!expiredTurns.Contains(done))
                Assert.Contains(effectCalls, call => call.Done == done && call.AfterRoll == doneAtRoll[done - 1]
                    && call.Slot == pushCue);

        // The wrapper passes each request on to the helper while effects are enabled and none while
        // they are not (FND-AUDIO-002, FND-AUDIO-019). Otherwise only a direct call, such as the
        // turn-start cue's (BUG-AUDIO-001), reaches the helper, and the last check finds none.
        var passedOn = calls.Where(call => call.Call == WrapperHelperCall).ToList();
        if (effectsEnabled)
            Assert.Equal(effectCalls.Select(call => (call.AfterRoll, call.Done, call.Slot)),
                passedOn.Select(call => (call.AfterRoll, call.Done, call.Slot)));
        else
            Assert.Empty(passedOn);
        Assert.DoesNotContain(calls, call => call.Slot == GeneralSoundSlot.TurnStartCue);
    }

    // The check refuses, on made-up calls, a run that cannot show its recording ran or that the
    // setting held: wrapper calls or level setups missing, a level setup that changed the setting
    // or a first one outside the title initialization, a request passed on with effects off or not
    // passed on with effects on, and the turn-start cue.
    [Fact]
    public void TheSoundCallCheckRefusesARunWithoutProof()
    {
        int[] doneAtRoll = [10, 20];
        var expired = new HashSet<int>();
        var pushCue = AudioRouting.PointerPushSound();
        RecordedSoundCall[] requests = [new(0, 0, pushCue, 0), new(10, 1, pushCue, 0), new(20, 2, pushCue, 0)];
        RecordedSoundCall[] passedOn = [.. requests.Select(call => call with { Call = WrapperHelperCall })];
        RecordedLevelSetup[] off = [new(0, 0, TitleLevelSetupCall, false)];
        RecordedLevelSetup[] on = [new(0, 0, TitleLevelSetupCall, true)];
        var turnStart = new RecordedSoundCall(10, 1, GeneralSoundSlot.TurnStartCue, 0);

        AssertSoundCalls([], requests, off, effectsEnabled: false, doneAtRoll, expired);
        AssertSoundCalls(passedOn, requests, on, effectsEnabled: true, doneAtRoll, expired);

        static void Refused(Action check) => Assert.ThrowsAny<Xunit.Sdk.XunitException>(check);
        Refused(() => AssertSoundCalls([], [], off, effectsEnabled: false, doneAtRoll, expired));
        Refused(() => AssertSoundCalls([], requests[..2], off, effectsEnabled: false, doneAtRoll, expired));
        Refused(() => AssertSoundCalls([], requests, [], effectsEnabled: false, doneAtRoll, expired));
        Refused(() => AssertSoundCalls([], requests, [.. off, new(15, 1, 0, true)], effectsEnabled: false, doneAtRoll, expired));
        Refused(() => AssertSoundCalls([], requests, [new(0, 0, 0, false)], effectsEnabled: false, doneAtRoll, expired));
        Refused(() => AssertSoundCalls(passedOn[..1], requests, off, effectsEnabled: false, doneAtRoll, expired));
        Refused(() => AssertSoundCalls(passedOn[..2], requests, on, effectsEnabled: true, doneAtRoll, expired));
        Refused(() => AssertSoundCalls([turnStart], requests, off, effectsEnabled: false, doneAtRoll, expired));
    }
}
