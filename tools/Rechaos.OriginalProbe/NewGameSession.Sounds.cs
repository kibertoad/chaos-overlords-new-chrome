namespace Rechaos.OriginalProbe;

/// <summary>
/// One call of the play helper (FND-AUDIO-006): the roll count and the Done presses before it, the
/// effect slot it was passed and the address of the call.
/// </summary>
internal sealed record SoundCallRecord(int AfterRoll, int Done, int Slot, uint Call);

internal sealed partial class NewGameSession
{
    private readonly List<SoundCallRecord> _soundCalls = [];

    // The values of effects_enabled the run read: at each call of the play helper, at each Done
    // press and at the end.
    private readonly HashSet<byte> _effectsEnabled = [];

    // Set once the preferences are loaded and the volumes written: before that the byte holds the
    // image's value, which no run plays under.
    private bool _preferencesSet;

    // RULE-AUDIO-006: with --sound-calls the probe records every call of the play helper
    // fn_0045851A(slot, priority), which the effects wrapper calls only while effects are enabled
    // and the turn-start cue calls directly (FND-AUDIO-006). Which calls a run can hold depends on
    // that setting, so the probe reads effects_enabled (FND-AUDIO-002) as well.
    private void ArmSounds() =>
        _process.SetBreakpoint(OriginalAddresses.PlayHelper, context =>
        {
            _soundCalls.Add(new SoundCallRecord(
                _rolls.Count, _rollsAtDone.Count, context.Argument(0), context.ReturnAddress - 5));
            SampleEffectsEnabled();
        }, quiet: true);

    // With effects off the wrapper never reaches the helper, so the read at each Done press is what
    // bounds every turn of such a run. A change that is undone between two reads is not seen.
    private void SampleEffectsEnabled() =>
        _effectsEnabled.Add(_process.Read(OriginalAddresses.EffectsEnabled, 1)[0]);

    // With --sound both volumes take their initialized values, effects 6 and music 5
    // (FND-OPTIONS-001), whatever the registry holds, so a run with sound does not depend on the
    // machine's preferences. They are written once the loader has read the registry, and the
    // title initialization's call of the level setup then derives effects_enabled, music_enabled
    // and the device volumes from them (FND-AUDIO-007), as Mute's zeros are. The flags are written
    // here as well, as Mute writes them, so they hold what the levels give even if the title
    // initialization's level setup ever runs before this write.
    private void Unmute()
    {
        _process.Write(OriginalAddresses.EffectsLevel, BitConverter.GetBytes(6));
        _process.Write(OriginalAddresses.MusicLevel, BitConverter.GetBytes(5));
        _process.Write(OriginalAddresses.EffectsEnabled, [1]);
        _process.Write(OriginalAddresses.MusicEnabled, [1]);
    }

    // Whether effects were enabled at every read of the run, read once more at its end: null when
    // the run stopped before the preferences were loaded, could not read the byte or read values
    // that disagree. Nothing the probe does after the preferences are loaded writes the byte.
    private bool? EffectsEnabledAtEachRead()
    {
        // A process that is exiting but has not reported its exit yet can refuse the read; the trace
        // is still written, with the values read at the calls.
        try
        {
            if (_preferencesSet && !_process.Exited) _effectsEnabled.Add(_process.Read(OriginalAddresses.EffectsEnabled, 1)[0]);
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            _notes.Add($"effects_enabled could not be read at the end of the run: {exception.Message}");
        }
        if (_effectsEnabled.Count > 1)
            _notes.Add($"effects_enabled changed during the run: {string.Join(", ", _effectsEnabled)}");
        return _effectsEnabled.Count == 1 ? _effectsEnabled.Single() != 0 : null;
    }
}
