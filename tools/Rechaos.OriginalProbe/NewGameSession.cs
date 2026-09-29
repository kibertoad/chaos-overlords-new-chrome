namespace Rechaos.OriginalProbe;

/// <summary>One call of roll(n): the call instruction's address, the bound and the result.</summary>
internal sealed record RollRecord(string Call, int Bound, int Result);

/// <summary>A human in a setup slot, with the name modifier its name is set to, if any.</summary>
internal sealed record HumanSlot(int Slot, string? Modifier);

/// <summary>
/// Setup choices the probe writes before Begin; a null leaves what the setup screen opened with.
/// Scenario numbers are the original's (FND-SETUP-013).
/// </summary>
internal sealed record NewGameSettings(int? Scenario, int? Mentality, int? TurnLimit, IReadOnlyList<HumanSlot>? Humans)
{
    public static readonly NewGameSettings Defaults = new(null, null, null, null);

    public IEnumerable<string> Describe()
    {
        if (Scenario is { } scenario) yield return $"scenario {scenario}";
        if (Mentality is { } mentality) yield return $"mentality {mentality}";
        if (TurnLimit is { } turns) yield return $"turn_limit {turns}";
        if (Humans is null) yield break;
        foreach (var human in Humans)
            yield return human.Modifier is null
                ? $"slot {human.Slot}: human"
                : $"slot {human.Slot}: human named modifier_name_{human.Modifier}";
    }
}

internal sealed record ProbeTrace(
    string Executable,
    NewGameSettings? Settings,
    int Seed,
    List<RollRecord> Rolls,
    int RollsBeforeBegin,
    bool Dumped,
    List<string> Notes);

/// <summary>
/// Starts the original in a window, records the seed and every roll, opens a new local game with
/// the given settings and dumps the writable sections once the first planning phase waits for the
/// first human.
/// </summary>
internal sealed class NewGameSession(
    string executable, string gameDirectory, string outputDirectory, TimeSpan timeout, NewGameSettings settings)
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
        ApplySettings();
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

    // Writes what the setup screen's controls would have committed (FND-SETUP-013): the screen is
    // idle in its message loop, and Begin reads these globals.
    private void ApplySettings()
    {
        if (settings.Scenario is { } scenario)
        {
            _process.Write(OriginalAddresses.Scenario, BitConverter.GetBytes(scenario));
            _process.Write(OriginalAddresses.PreferredScenario, [(byte)scenario]);
        }

        if (settings.TurnLimit is { } turns) _process.Write(OriginalAddresses.TurnLimit, BitConverter.GetBytes(turns));
        if (settings.Mentality is { } mentality) _process.Write(OriginalAddresses.Mentality, [(byte)mentality]);
        if (settings.Humans is null) return;

        // Humans take portraits 0, 1, 2... in slot order, as Add gives the lowest free one; an
        // empty slot has type -1 and portrait 15. A plain human keeps the name the slot holds.
        var portrait = (byte)0;
        for (var slot = 0; slot < 6; slot++)
        {
            var human = settings.Humans.FirstOrDefault(entry => entry.Slot == slot);
            _process.Write(OriginalAddresses.RosterTypes + (uint)(4 * slot), BitConverter.GetBytes(human is null ? -1 : 0));
            _process.Write(OriginalAddresses.RosterPortraits + (uint)slot, [human is null ? OriginalAddresses.EmptyPortrait : portrait++]);
            if (human?.Modifier is { } modifier)
                _process.Write(OriginalAddresses.RosterNames + (uint)(OriginalAddresses.RosterNameLength * slot), ModifierName(modifier));
        }
    }

    // The modifier string as a 12-byte length-prefixed name, read from the running executable so
    // the probe carries none of them (FND-SETUP-015).
    private byte[] ModifierName(string modifier)
    {
        var raw = _process.Read(OriginalAddresses.ModifierNames[modifier] + 1, OriginalAddresses.RosterNameLength - 1);
        var length = Array.IndexOf(raw, (byte)0);
        if (length < 0) throw new InvalidDataException($"modifier_name_{modifier} has no terminator.");
        var name = new byte[OriginalAddresses.RosterNameLength];
        name[0] = (byte)length;
        Array.Copy(raw, 0, name, 1, length);
        return name;
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
        return new ProbeTrace(executable, settings, _seed, _rolls, rollsBeforeBegin, dumped, _notes);
    }

    private static void Click(IntPtr window, int x, int y)
    {
        var position = (IntPtr)((y << 16) | (x & 0xFFFF));
        Native.PostMessageW(window, Native.WmMouseMove, IntPtr.Zero, position);
        Native.PostMessageW(window, Native.WmLButtonDown, 1, position);
        Native.PostMessageW(window, Native.WmLButtonUp, IntPtr.Zero, position);
    }
}
