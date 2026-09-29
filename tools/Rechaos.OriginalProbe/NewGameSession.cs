namespace Rechaos.OriginalProbe;

/// <summary>One call of roll(n): the call instruction's address, the bound and the result.</summary>
internal sealed record RollRecord(string Call, int Bound, int Result);

internal sealed record ProbeTrace(
    string Executable,
    int Seed,
    List<RollRecord> Rolls,
    int RollsBeforeBegin,
    bool Dumped,
    List<string> Notes);

/// <summary>
/// Starts the original in a window, records the seed and every roll, opens a new local game with
/// the setup screen's defaults and dumps the writable sections once the first planning phase waits
/// for the human.
/// </summary>
internal sealed class NewGameSession(string executable, string gameDirectory, string outputDirectory, TimeSpan timeout)
    : IDisposable
{
    private readonly OriginalProcess _process = OriginalProcess.Start(executable, gameDirectory);
    private readonly List<RollRecord> _rolls = [];
    private readonly List<string> _notes = [];
    private int _seed = -1;
    private bool _setupReached;

    public ProbeTrace Run()
    {
        _process.SetBreakpoint(OriginalAddresses.SeedGenerator, context => _seed = context.Argument(0));
        _process.SetBreakpoint(OriginalAddresses.Roll, OnRoll);
        _process.SetBreakpoint(OriginalAddresses.PreferenceLoaderCall + 5, ForceWindow, oneShot: true);
        _process.SetBreakpoint(OriginalAddresses.LocalSetup, _ => _setupReached = true);

        var window = IntPtr.Zero;
        if (!_process.RunUntil(() => (window = _process.FindMainWindow()) != IntPtr.Zero, timeout))
            return Finish(false, "The game window never appeared.");

        // The logos and intro movies end on the left button; the title then takes File, New Game.
        var nextPoke = DateTime.MinValue;
        var reached = _process.RunUntil(() =>
        {
            if (_setupReached) return true;
            if (DateTime.UtcNow < nextPoke) return false;
            nextPoke = DateTime.UtcNow.AddSeconds(1.5);
            Click(window, 320, 240);
            Native.PostMessageW(window, Native.WmCommand, OriginalAddresses.NewGameCommand, IntPtr.Zero);
            return false;
        }, timeout);
        if (!reached) return Finish(false, "The setup screen never opened.");

        _process.Pump(TimeSpan.FromSeconds(2));
        var rollsBeforeBegin = _rolls.Count;
        Click(window, OriginalAddresses.BeginX, OriginalAddresses.BeginY);

        // Planning has begun when the rolls of the new match have stopped for a while.
        var settled = _process.RunUntil(
            () => _rolls.Count > rollsBeforeBegin
                  && DateTime.UtcNow - _process.LastBreakpointUtc > TimeSpan.FromSeconds(8),
            timeout);
        if (!settled) return Finish(false, "The new match never settled.", rollsBeforeBegin);

        DumpWritableSections();
        return Finish(true, null, rollsBeforeBegin);
    }

    public void Dispose() => _process.Dispose();

    private void OnRoll(BreakContext context)
    {
        var call = context.ReturnAddress - 5;
        var bound = context.Argument(0);
        _process.SetBreakpoint(context.ReturnAddress, returned =>
            _rolls.Add(new RollRecord($"0x{call:X8}", bound, (int)returned.Eax)), oneShot: true);
    }

    private void ForceWindow(BreakContext context)
    {
        var call = _process.Read(OriginalAddresses.PreferenceLoaderCall, 1)[0];
        if (call != 0xE8) _notes.Add($"The preference loader call starts with 0x{call:X2}, not a call.");
        _process.Write(OriginalAddresses.PrefFullScreen, [0]);
        _process.Write(OriginalAddresses.PrefFullScreenCopy, [0]);
    }

    private void DumpWritableSections()
    {
        foreach (var section in PeSection.Read(executable, out _).Where(section => section.IsWritable))
        {
            var bytes = _process.Read(section.VirtualAddress, (int)section.VirtualSize);
            var name = $"{section.Name.TrimStart('.')}-{section.VirtualAddress:X8}.bin";
            File.WriteAllBytes(Path.Combine(outputDirectory, name), bytes);
            _notes.Add($"Dumped {section.Name} at 0x{section.VirtualAddress:X8}, {section.VirtualSize} bytes, to {name}.");
        }
    }

    private ProbeTrace Finish(bool dumped, string? note, int rollsBeforeBegin = 0)
    {
        if (note is not null) _notes.Add(note);
        _notes.AddRange(_process.Log);
        if (_process.Exited) _notes.Add($"The process exited with code 0x{_process.ExitCode:X8}.");
        return new ProbeTrace(executable, _seed, _rolls, rollsBeforeBegin, dumped, _notes);
    }

    private static void Click(IntPtr window, int x, int y)
    {
        var position = (IntPtr)((y << 16) | (x & 0xFFFF));
        Native.PostMessageW(window, Native.WmMouseMove, IntPtr.Zero, position);
        Native.PostMessageW(window, Native.WmLButtonDown, 1, position);
        Native.PostMessageW(window, Native.WmLButtonUp, IntPtr.Zero, position);
    }
}
