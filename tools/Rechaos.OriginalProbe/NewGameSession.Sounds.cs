namespace Rechaos.OriginalProbe;

/// <summary>
/// One call of the play helper (FND-AUDIO-006): the roll count and the Done presses before it, the
/// effect slot it was passed and the address of the call.
/// </summary>
internal sealed record SoundCallRecord(int AfterRoll, int Done, int Slot, uint Call);

internal sealed partial class NewGameSession
{
    private readonly List<SoundCallRecord> _soundCalls = [];

    // RULE-AUDIO-006: with --sounds the probe records every call of the play helper
    // fn_0045851A(slot, priority), which the effects wrapper calls only while effects are enabled
    // and the turn-start cue calls directly (FND-AUDIO-006).
    private void ArmSounds() =>
        _process.SetBreakpoint(OriginalAddresses.PlayHelper, context => _soundCalls.Add(new SoundCallRecord(
            _rolls.Count, _rollsAtDone.Count, context.Argument(0), context.ReturnAddress - 5)), quiet: true);
}
