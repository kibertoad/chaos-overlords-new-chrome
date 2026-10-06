namespace Rechaos.OriginalProbe;

/// <summary>
/// One call of the play helper (FND-AUDIO-006): the roll count and the Done presses before it, the
/// effect slot it was passed and the address of the call.
/// </summary>
internal sealed record SoundCallRecord(int AfterRoll, int Done, int Slot, uint Call);

internal sealed partial class NewGameSession
{
    private readonly List<SoundCallRecord> _soundCalls = [];

    // The values of effects_enabled the run read: at each call of the play helper and at the end.
    private readonly HashSet<byte> _effectsEnabled = [];

    // RULE-AUDIO-006: with --sound-calls the probe records every call of the play helper
    // fn_0045851A(slot, priority), which the effects wrapper calls only while effects are enabled
    // and the turn-start cue calls directly (FND-AUDIO-006). Which calls a run can hold depends on
    // that setting, so the probe reads effects_enabled (FND-AUDIO-002) as well.
    private void ArmSounds() =>
        _process.SetBreakpoint(OriginalAddresses.PlayHelper, context =>
        {
            _soundCalls.Add(new SoundCallRecord(
                _rolls.Count, _rollsAtDone.Count, context.Argument(0), context.ReturnAddress - 5));
            _effectsEnabled.Add(_process.Read(OriginalAddresses.EffectsEnabled, 1)[0]);
        }, quiet: true);

    // With --sound both volumes take their initialized values, effects 6 and music 5
    // (FND-OPTIONS-001), whatever the registry holds, so a run with sound does not depend on the
    // machine's preferences. They are written once the loader has read the registry, and the
    // title initialization's call of the level setup then derives effects_enabled, music_enabled
    // and the device volumes from them (FND-AUDIO-007), as Mute's zeros are.
    private void Unmute()
    {
        _process.Write(OriginalAddresses.EffectsLevel, BitConverter.GetBytes(6));
        _process.Write(OriginalAddresses.MusicLevel, BitConverter.GetBytes(5));
    }

    // Whether effects were enabled for the whole run, read once more at its end: null when the run
    // could not read the byte or read values that disagree, so a fixture never claims a setting the
    // run did not keep. Nothing the probe does after the preferences are loaded writes the byte.
    private bool? EffectsEnabledThroughout()
    {
        if (!_process.Exited) _effectsEnabled.Add(_process.Read(OriginalAddresses.EffectsEnabled, 1)[0]);
        if (_effectsEnabled.Count > 1)
            _notes.Add($"effects_enabled changed during the run: {string.Join(", ", _effectsEnabled)}");
        return _effectsEnabled.Count == 1 ? _effectsEnabled.Single() != 0 : null;
    }
}
