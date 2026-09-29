namespace Rechaos.OriginalProbe;

/// <summary>One call of roll(n): the call instruction's address, the bound and the result.</summary>
internal sealed record RollRecord(string Call, int Bound, int Result);

/// <summary>A human in a setup slot, with the name modifier its name is set to, if any.</summary>
internal sealed record HumanSlot(int Slot, string? Modifier);

/// <summary>
/// An order the probe writes into a gang record of the first human before the Done press of
/// <paramref name="Turn"/>, counted from 1: the FMT-STATE-001 bytes <c>action</c>, <c>target</c>
/// and <c>target_2</c>, and for a recurring order <c>repeat_action</c> and <c>repeat_target</c>, as
/// RULE-TURN-005 has the order screens write them.
/// </summary>
internal sealed record ProbeOrder(int Turn, int Slot, int Action, int Target, int Target2, bool Repeat)
{
    public override string ToString() =>
        $"turn {Turn}: gang slot {Slot} action {Action} target {Target} target_2 {Target2} repeat {(Repeat ? 1 : 0)}";
}

/// <summary>
/// Setup choices the probe writes before Begin; a null leaves what the setup screen opened with.
/// Scenario numbers are the original's (FND-SETUP-013).
/// </summary>
internal sealed record NewGameSettings(
    int? Scenario, int? Mentality, int? TurnLimit, IReadOnlyList<HumanSlot>? Humans, int EndTurns = 0,
    bool TraceHires = false, int? Seed = null, int? DumpAtRoll = null, uint? TraceCalls = null,
    IReadOnlyList<ProbeOrder>? Orders = null)
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

    /// <summary>
    /// The orders and Done presses after the first planning phase, one input each: an order is
    /// named <c>order</c> and a Done press <c>left_click</c>.
    /// </summary>
    public IEnumerable<(string Name, string Value)> DescribeTurns()
    {
        for (var turn = 1; turn <= EndTurns; turn++)
        {
            var orders = (Orders ?? []).Where(order => order.Turn == turn).ToArray();
            foreach (var order in orders) yield return ("order", order.ToString());
            yield return ("left_click", orders.Length == 0
                ? $"Done (550, 306) with no orders, turn {turn}"
                : $"Done (550, 306), turn {turn}");
        }
    }
}

internal sealed record ProbeTrace(
    string Executable,
    NewGameSettings? Settings,
    int Seed,
    List<RollRecord> Rolls,
    int RollsBeforeBegin,
    List<int>? RollsAtDone,
    bool Dumped,
    List<string> Notes);

/// <summary>
/// Starts the original in a window, records the seed and every roll, opens a new local game with
/// the given settings, presses Done as many times as asked, and dumps the writable sections once
/// the planning phase that follows waits for the first human.
/// </summary>
internal sealed class NewGameSession(
    string executable, string gameDirectory, string outputDirectory, TimeSpan timeout, NewGameSettings settings)
    : IDisposable
{
    private readonly OriginalProcess _process = OriginalProcess.Start(executable, gameDirectory);
    private readonly List<RollRecord> _rolls = [];
    private readonly List<string> _notes = [];
    private readonly List<int> _rollsAtDone = [];
    private int _seed = -1;
    private bool _setupReached;
    private int _panelsOpen;

    public ProbeTrace Run()
    {
        _process.SetBreakpoint(OriginalAddresses.SeedGenerator, SeedGenerator);
        _process.SetBreakpoint(OriginalAddresses.Roll, OnRoll);
        _process.SetBreakpoint(OriginalAddresses.PreferenceLoaderCall + 5, ForceWindow, oneShot: true);
        _process.SetBreakpoint(OriginalAddresses.LocalSetup, _ => _setupReached = true);
        if (settings.TraceHires) _process.SetBreakpoint(OriginalAddresses.HireOrderCheck, TraceHire);
        if (settings.TraceCalls is { } traced) _process.SetBreakpoint(traced, TraceCall);
        _process.SetBreakpoint(OriginalAddresses.CombatResults, context => OpenPanel(context, "Combat Results"));
        _process.SetBreakpoint(OriginalAddresses.LastTurnEvents, context => OpenPanel(context, "Last Turn Events"));

        var window = IntPtr.Zero;
        if (!_process.RunUntil(() => (window = _process.FindMainWindow()) != IntPtr.Zero, timeout))
            return Finish(false, "The game window never appeared.");

        // RULE-VIDEO-001: a movie ends when left_button_down is set at one of its 10 Hz ticks, so a
        // posted press and release is missed. The probe holds the button in memory until the setup
        // screen opens; the title then takes File, New Game.
        var nextPoke = DateTime.MinValue;
        var reached = _process.RunUntil(() =>
        {
            if (_setupReached) return true;
            if (DateTime.UtcNow < nextPoke) return false;
            nextPoke = DateTime.UtcNow.AddSeconds(0.5);
            _process.Write(OriginalAddresses.LeftButtonDown, [1]);
            Native.PostMessageW(window, Native.WmCommand, OriginalAddresses.NewGameCommand, IntPtr.Zero);
            return false;
        }, timeout);
        _process.Write(OriginalAddresses.LeftButtonDown, [0]);
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

        // Each Done ends the human's planning with no orders; the next planning phase has begun
        // when elapsed_turns has moved on and the rolls have stopped again.
        for (var turn = 1; turn <= settings.EndTurns; turn++)
        {
            if (!ClosePanels(window))
                return Finish(false, $"A panel of turn {turn} never closed.", rollsBeforeBegin);
            foreach (var order in (settings.Orders ?? []).Where(order => order.Turn == turn))
                WriteOrder(order);
            _rollsAtDone.Add(_rolls.Count);
            Click(window, OriginalAddresses.DoneX, OriginalAddresses.DoneY);
            var target = turn;
            var rollsAtClick = _rolls.Count;
            var clicked = DateTime.UtcNow;
            // A press the game did not take leaves the turn where it was with no roll made; press
            // again after a quiet while.
            var next = _process.RunUntil(() =>
            {
                if (_rolls.Count == rollsAtClick && _process.ReadInt32(OriginalAddresses.ElapsedTurns) < target
                    && DateTime.UtcNow - clicked > TimeSpan.FromSeconds(20))
                {
                    ClosePanels(window);
                    _notes.Add($"Done of turn {turn} pressed again after roll {_rolls.Count}");
                    Click(window, OriginalAddresses.DoneX, OriginalAddresses.DoneY);
                    clicked = DateTime.UtcNow;
                }
                return _process.ReadInt32(OriginalAddresses.ElapsedTurns) >= target
                       && DateTime.UtcNow - _process.LastBreakpointUtc > TimeSpan.FromSeconds(8);
            }, timeout);
            if (!next) return Finish(false, $"Turn {turn} never reached the next planning phase.", rollsBeforeBegin);
        }

        DumpWritableSections();
        return Finish(true, null, rollsBeforeBegin);
    }

    public void Dispose() => _process.Dispose();

    private void OpenPanel(BreakContext context, string panel)
    {
        _panelsOpen++;
        _notes.Add($"{panel} opened after roll {_rolls.Count}");
        _process.SetBreakpoint(context.ReturnAddress, _ => _panelsOpen--, oneShot: true);
    }

    // Presses Exit until every panel handler that opened has returned.
    private bool ClosePanels(IntPtr window)
    {
        for (var attempt = 0; _panelsOpen > 0 && attempt < 10; attempt++)
        {
            Click(window, OriginalAddresses.PanelExitX, OriginalAddresses.PanelExitY);
            _process.RunUntil(() => _panelsOpen == 0, TimeSpan.FromSeconds(3));
        }

        return _panelsOpen == 0;
    }

    private void WriteOrder(ProbeOrder order)
    {
        var human = settings.Humans is { Count: > 0 } humans ? humans[0].Slot : 0;
        var record = OriginalAddresses.GangRecords
            + (uint)(human * OriginalAddresses.PlayerGangStride + order.Slot * OriginalAddresses.GangRecordSize);
        _process.Write(record + 7, [
            (byte)order.Action, (byte)order.Target, (byte)order.Target2,
            (byte)(order.Repeat ? order.Action : 0), (byte)(order.Repeat ? order.Target : 0)]);
        _notes.Add($"order after roll {_rolls.Count}: {order}");
    }

    // --seed replaces the clock value the process start passes to srand, so a run can be repeated.
    private void SeedGenerator(BreakContext context)
    {
        if (settings.Seed is { } seed) _process.Write(context.Esp + 4, BitConverter.GetBytes(seed));
        _seed = context.Argument(0);
    }

    // A diagnostic dump, not fixture data: the writable sections and the stack as roll(n) is
    // entered for the given zero-based roll, with the registers in a note.
    private void DumpAtRoll(BreakContext context)
    {
        var directory = Path.Combine(outputDirectory, $"at-roll-{_rolls.Count}");
        Directory.CreateDirectory(directory);
        foreach (var section in PeSection.Read(executable, out _).Where(section => section.IsWritable))
            File.WriteAllBytes(
                Path.Combine(directory, $"{section.Name.TrimStart('.')}-{section.VirtualAddress:X8}.bin"),
                _process.Read(section.VirtualAddress, (int)section.VirtualSize));
        for (var length = 0x4000; length >= 0x400; length /= 2)
        {
            try
            {
                File.WriteAllBytes(Path.Combine(directory, $"stack-{context.Esp:X8}.bin"), _process.Read(context.Esp, length));
                break;
            }
            catch (System.ComponentModel.Win32Exception) { }
        }
        _notes.Add($"Dumped at roll {_rolls.Count}: esp 0x{context.Esp:X8} ebp 0x{context.Ebp:X8}, return 0x{context.ReturnAddress:X8}.");
    }

    private void OnRoll(BreakContext context)
    {
        if (settings.DumpAtRoll == _rolls.Count) DumpAtRoll(context);
        var call = context.ReturnAddress - 5;
        var bound = context.Argument(0);
        _process.SetBreakpoint(context.ReturnAddress, returned =>
            _rolls.Add(new RollRecord($"0x{call:X8}", bound, (int)returned.Eax)), oneShot: true);
    }

    // A diagnostic note, not fixture data: the offers, orders and cash each time the hire block
    // checks an order, with the roll count so far.
    private void TraceHire(BreakContext context)
    {
        var offers = _process.Read(OriginalAddresses.HireOffers, 18).Select(value => (int)(sbyte)value);
        var orders = _process.Read(OriginalAddresses.HireOrders, 18).Select(value => (int)(sbyte)value);
        var cash = Enumerable.Range(0, 6).Select(player => _process.ReadInt32(OriginalAddresses.Cash + (uint)(4 * player)));
        _notes.Add($"hire check after roll {_rolls.Count}: offers [{string.Join(",", offers)}] orders [{string.Join(",", orders)}] cash [{string.Join(",", cash)}]");
    }

    // A diagnostic note, not fixture data: each call of the traced function with the roll count so
    // far, its caller, its first four stack arguments and what it returned.
    private void TraceCall(BreakContext context)
    {
        var call = context.ReturnAddress - 5;
        var arguments = string.Join(",", Enumerable.Range(0, 4).Select(index => context.Argument(index)));
        var rolls = _rolls.Count;
        _process.SetBreakpoint(context.ReturnAddress, returned =>
            _notes.Add($"call after roll {rolls} from 0x{call:X8} with [{arguments}] returned {(int)returned.Eax}"), oneShot: true);
    }

    private void ForceWindow(BreakContext context)
    {
        var call = _process.Read(OriginalAddresses.PreferenceLoaderCall, 1)[0];
        if (call != 0xE8) _notes.Add($"The preference loader call starts with 0x{call:X2}, not a call.");
        _process.Write(OriginalAddresses.PrefFullScreen, [0]);
        _process.Write(OriginalAddresses.PrefFullScreenCopy, [0]);
        if (settings.EndTurns == 0) return;
        _process.Write(OriginalAddresses.PrefWarnIdle, BitConverter.GetBytes(0));
        _process.Write(OriginalAddresses.PrefDetailedCombat, BitConverter.GetBytes(0));
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
        return new ProbeTrace(executable, settings, _seed, _rolls, rollsBeforeBegin, _rollsAtDone, dumped, _notes);
    }

    private static void Click(IntPtr window, int x, int y)
    {
        var position = (IntPtr)((y << 16) | (x & 0xFFFF));
        Native.PostMessageW(window, Native.WmMouseMove, IntPtr.Zero, position);
        Native.PostMessageW(window, Native.WmLButtonDown, 1, position);
        Native.PostMessageW(window, Native.WmLButtonUp, IntPtr.Zero, position);
    }
}
