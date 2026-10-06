namespace Rechaos.OriginalProbe;

/// <summary>
/// One close of the window after the dump (RULE-UI-015): <c>match_saved</c> and the byte that marks
/// no match in play as the close was posted, the answer the probe gave, the dialogs the game opened,
/// how many times it called the save, and the store of <c>quit_requested</c> it reached, or 0.
/// </summary>
internal sealed record CloseRecord(int Saved, int NoMatch, int Answer, int SaveResult, List<int> Dialogs)
{
    public int Saves { get; set; }
    public uint LeftAt { get; set; }
}

/// <summary>
/// A close after the dump: <paramref name="Saved"/> is written to <c>match_saved</c> first when it
/// is 0 or 1, and <paramref name="Answer"/> is what the dialog the close opens returns: 1 save first,
/// 2 cancel, 3 go on without saving (FND-UI-022). For answer 1, <paramref name="SaveResult"/> is what
/// the save returns: 1 written, 0 cancelled.
/// </summary>
internal sealed record ProbeClose(int? Saved, int Answer, int SaveResult = -1)
{
    public override string ToString() =>
        $"close {(Saved is { } saved ? saved.ToString() : "-")}:{Answer}{(Answer == 1 ? SaveResult == 1 ? "w" : "c" : "")}";
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
    // from the call, as the dialog procedure would for that button. The save fn_00463CC5, which
    // takes no arguments, is not run either: the probe returns the step's save result from it. The
    // three stores of quit_requested in File, Exit are skipped and recorded, so the game stays in
    // planning and every close of a run is recorded.
    private string? RecordCloses(IntPtr window)
    {
        CloseRecord? current = null;
        void Return(BreakContext context, uint value)
        {
            context.Eax = value;
            context.Eip = context.ReturnAddress;
            context.Esp += 4;
        }
        _process.SetBreakpoint(OriginalAddresses.DialogOpen, context =>
        {
            if (current is null) return;
            current.Dialogs.Add(context.Argument(0));
            Return(context, (uint)current.Answer);
        });
        _process.SetBreakpoint(OriginalAddresses.SaveGame, context =>
        {
            if (current is null) return;
            current.Saves++;
            Return(context, (uint)Math.Max(0, current.SaveResult));
        });
        foreach (var store in OriginalAddresses.ExitQuitStores)
            _process.SetBreakpoint(store, context =>
            {
                if (current is null) return;
                current.LeftAt = store;
                context.Eip += OriginalAddresses.ExitQuitStoreLength;
            });
        foreach (var step in settings.Closes!)
        {
            _postDumpStep++;
            if (step.Saved is { } saved) _process.Write(OriginalAddresses.MatchSaved, [(byte)saved]);
            current = new CloseRecord(_process.Read(OriginalAddresses.MatchSaved, 1)[0],
                _process.Read(OriginalAddresses.NoMatchInPlay, 1)[0], step.Answer, step.SaveResult, []);
            _closes.Add(current);
            Native.PostMessageW(window, Native.WmClose, IntPtr.Zero, IntPtr.Zero);
            var posted = DateTime.UtcNow;
            var record = current;
            _process.RunUntil(() => record.LeftAt != 0 || DateTime.UtcNow - posted > TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(10));
            _notes.Add($"{step}: dialogs [{string.Join(",", current.Dialogs)}], saves {current.Saves}, "
                       + $"left at 0x{current.LeftAt:X8}");
            if (_process.Exited) return $"the game exited at {step}";
        }
        current = null;
        return null;
    }
}
