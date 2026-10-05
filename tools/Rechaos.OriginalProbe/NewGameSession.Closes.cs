namespace Rechaos.OriginalProbe;

/// <summary>
/// One close of the window after the dump (RULE-UI-015): <c>match_saved</c> and the byte that marks
/// no match in play as the close was posted, the dialogs the game opened, the answer the probe gave
/// each, and whether the game then set <c>quit_requested</c> or exited.
/// </summary>
internal sealed record CloseRecord(int Saved, int NoMatch, int Answer, List<int> Dialogs)
{
    public bool QuitRequested { get; set; }
    public bool Exited { get; set; }
}

/// <summary>
/// A close after the dump: <paramref name="Saved"/> is written to <c>match_saved</c> first when it
/// is 0 or 1, and <paramref name="Answer"/> is what the dialog the close opens returns: 1 save first,
/// 2 cancel, 3 go on without saving (FND-UI-022).
/// </summary>
internal sealed record ProbeClose(int? Saved, int Answer)
{
    public override string ToString() => $"close {(Saved is { } saved ? saved.ToString() : "-")}:{Answer}";
}

/// <summary>A write of <c>match_saved</c> before the Done press of <paramref name="Turn"/>, or at the
/// dump when the turn is one past the last.</summary>
internal sealed record ProbeSavedWrite(int Turn, int Value)
{
    public override string ToString() => $"turn {Turn}: match_saved {Value}";
}

/// <summary>A write of <c>match_saved</c>, with the value the game held just before it.</summary>
internal sealed record SavedWriteRecord(int Turn, int Before, int Value);

internal sealed partial class NewGameSession
{
    private readonly List<CloseRecord> _closes = [];
    private readonly List<SavedWriteRecord> _savedWrites = [];

    private void WriteSaved(ProbeSavedWrite write)
    {
        _savedWrites.Add(new SavedWriteRecord(write.Turn, _process.Read(OriginalAddresses.MatchSaved, 1)[0], write.Value));
        _process.Write(OriginalAddresses.MatchSaved, [(byte)write.Value]);
        _notes.Add($"saved after roll {_rolls.Count}: {write}");
    }

    // RULE-UI-015, FND-UI-058: a close of the window is File, Exit (RULE-UI-014). Each dialog the game
    // opens through fn_00465CEC is answered without showing it: the probe returns the step's answer
    // from the call, as the dialog procedure would for that button. The save the first answer starts,
    // fn_00463CC5, opens the save dialog, so no step answers 1.
    private string? RecordCloses(IntPtr window)
    {
        CloseRecord? current = null;
        _process.SetBreakpoint(OriginalAddresses.DialogOpen, context =>
        {
            if (current is null) return;
            current.Dialogs.Add(context.Argument(0));
            context.Eax = (uint)current.Answer;
            context.Eip = context.ReturnAddress;
            context.Esp += 4;
        });
        foreach (var step in settings.Closes!)
        {
            _postDumpStep++;
            if (step.Saved is { } saved) _process.Write(OriginalAddresses.MatchSaved, [(byte)saved]);
            current = new CloseRecord(_process.Read(OriginalAddresses.MatchSaved, 1)[0],
                _process.Read(OriginalAddresses.NoMatchInPlay, 1)[0], step.Answer, []);
            _closes.Add(current);
            Native.PostMessageW(window, Native.WmClose, IntPtr.Zero, IntPtr.Zero);
            var posted = DateTime.UtcNow;
            _process.RunUntil(() =>
                _process.Read(OriginalAddresses.QuitRequested, 1)[0] != 0
                || DateTime.UtcNow - posted > TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));
            current.QuitRequested = !_process.Exited && _process.Read(OriginalAddresses.QuitRequested, 1)[0] != 0;
            _notes.Add($"{step}: dialogs [{string.Join(",", current.Dialogs)}], quit_requested {(current.QuitRequested ? 1 : 0)}");
            if (current.QuitRequested)
            {
                _process.RunUntil(() => _process.Exited, TimeSpan.FromSeconds(15));
                current.Exited = _process.Exited;
                return null;
            }
            if (_process.Exited)
            {
                current.Exited = true;
                return null;
            }
        }
        return null;
    }
}
