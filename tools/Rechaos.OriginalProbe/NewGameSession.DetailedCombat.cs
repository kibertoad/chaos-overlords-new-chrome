namespace Rechaos.OriginalProbe;

/// <summary>
/// One clip Detailed Combat played (FND-COMBAT-011): the roll count it came after, the focal gang's
/// element number, the other element's (-2 for the police), the clip player's argument, the right
/// ends the presenter gave the two bars, the sound numbers loaded into slot 5 since the clip before
/// (FND-AUDIO-013), and whether the clip played slot 5.
/// </summary>
internal sealed record CombatClipRecord(
    int AfterRoll, int Focal, int Other, int Hold, int FocalBar, int OtherBar, List<int> Sounds)
{
    public bool Played { get; set; }
}

/// <summary>
/// One call of the presentation (FND-COMBAT-010): the roll count it came after, its second argument
/// (1 when planning opened it, 0 when the console's control did), the index of its first clip in the
/// clip list, the clips it played and the effect slots it played itself.
/// </summary>
internal sealed record CombatPresentationRecord(int AfterRoll, int Automatic, int FirstClip, List<int> Sounds)
{
    public int Clips { get; set; }
}

internal sealed partial class NewGameSession
{
    private readonly List<CombatClipRecord> _combatClips = [];
    private readonly List<CombatPresentationRecord> _combatPresentations = [];
    private readonly List<int> _clipSounds = [];
    private bool _detailedCombatOpen;

    // RULE-COMBAT-004, RULE-AUDIO-009: with --detailed-combat the human's planning opens the
    // presentation fn_0042E040, which plays every clip with fn_00430C23 and loads the attack sound
    // with the loader fn_0045867C(slot 5, number) (FND-COMBAT-011, FND-AUDIO-013, FND-AUDIO-006).
    // The presentation plays to its end by itself, and the turn loop waits until it returns.
    private void ArmDetailedCombat()
    {
        _process.SetBreakpoint(OriginalAddresses.DetailedCombat, context =>
        {
            _detailedCombatOpen = true;
            _clipSounds.Clear();
            var presentation = new CombatPresentationRecord(_rolls.Count, context.Argument(1), _combatClips.Count, []);
            _combatPresentations.Add(presentation);
            _notes.Add($"Detailed Combat opened after roll {_rolls.Count} with flag {presentation.Automatic}");
            _process.SetBreakpoint(context.ReturnAddress, _ =>
            {
                _detailedCombatOpen = false;
                presentation.Clips = _combatClips.Count - presentation.FirstClip;
            }, oneShot: true);
        });
        _process.SetBreakpoint(OriginalAddresses.SoundLoader, context =>
        {
            if (_detailedCombatOpen && context.Argument(0) == 5) _clipSounds.Add(context.Argument(1));
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.CombatClip, context =>
        {
            _combatClips.Add(new CombatClipRecord(_rolls.Count,
                _process.ReadInt16(OriginalAddresses.CombatFocal), _process.ReadInt16(OriginalAddresses.CombatOther),
                context.Argument(0), _process.ReadInt16(OriginalAddresses.CombatFocalBarRight),
                _process.ReadInt16(OriginalAddresses.CombatOtherBarRight), [.. _clipSounds]));
            _clipSounds.Clear();
        });
        _process.SetBreakpoint(OriginalAddresses.PlaySound, context =>
        {
            if (context.ReturnAddress is >= OriginalAddresses.CombatClip and <= OriginalAddresses.CombatClipEnd
                && context.Argument(0) == 5 && _combatClips.Count > 0)
                _combatClips[^1].Played = true;
            else if (context.ReturnAddress is >= OriginalAddresses.DetailedCombat and <= OriginalAddresses.DetailedCombatEnd
                && _combatPresentations.Count > 0)
                _combatPresentations[^1].Sounds.Add(context.Argument(0));
        }, quiet: true);
    }
}
