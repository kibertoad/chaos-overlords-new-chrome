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
/// A hire the human places before a Done press: the hire offer slot 0 to 2 and the sector it is
/// dropped on, written into <c>hire_orders</c> as the hire screen does (RULE-HIRE-003).
/// </summary>
internal sealed record ProbeHire(int Turn, int OfferSlot, int Sector)
{
    public override string ToString() => $"turn {Turn}: offer slot {OfferSlot} sector {Sector}";
}

/// <summary>
/// A computer player's planning state written before a Done press, for branches no local match
/// reaches: family 99 or less writes the <c>family</c> of the player's planning record in the slot
/// (FMT-STATE-007), and <see cref="Raider"/> sets the player's byte of <c>raider_mode</c>, which a
/// takeover of a network seat sets (RULE-AI-027).
/// </summary>
internal sealed record ProbePlanning(int Turn, int Player, int Slot, int Family)
{
    public const int Raider = -1;

    public override string ToString() => Family == Raider
        ? $"turn {Turn}: player {Player} raider_mode 1"
        : $"turn {Turn}: player {Player} gang slot {Slot} family {Family}";
}

/// <summary>
/// Setup choices the probe writes before Begin; a null leaves what the setup screen opened with.
/// Scenario numbers are the original's (FND-SETUP-013).
/// </summary>
internal sealed record NewGameSettings(
    int? Scenario, int? Mentality, int? TurnLimit, IReadOnlyList<HumanSlot>? Humans, int EndTurns = 0,
    bool TraceHires = false, int? Seed = null, int? DumpAtRoll = null, uint? TraceCalls = null,
    IReadOnlyList<ProbeOrder>? Orders = null, bool Sound = false, IReadOnlyList<ProbeHire>? Hires = null,
    IReadOnlyList<ProbePlanning>? Planning = null, int? TimeLimit = null, IReadOnlyList<int>? ExpireTurns = null)
{
    public static readonly NewGameSettings Defaults = new(null, null, null, null);

    public IEnumerable<string> Describe()
    {
        if (Scenario is { } scenario) yield return $"scenario {scenario}";
        if (Mentality is { } mentality) yield return $"mentality {mentality}";
        if (TurnLimit is { } turns) yield return $"turn_limit {turns}";
        if (TimeLimit is { } limit) yield return $"planning_limit_choice {limit}";
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
            var hires = (Hires ?? []).Where(hire => hire.Turn == turn).ToArray();
            var planning = (Planning ?? []).Where(write => write.Turn == turn).ToArray();
            foreach (var order in orders) yield return ("order", order.ToString());
            foreach (var hire in hires) yield return ("hire", hire.ToString());
            foreach (var write in planning) yield return ("planning", write.ToString());
            if (ExpireTurns?.Contains(turn) == true)
            {
                yield return ("wait", $"no Done press, turn {turn}: the planning time runs out");
                continue;
            }
            yield return ("left_click", orders.Length + hires.Length + planning.Length == 0
                ? $"Done (550, 306) with no orders, turn {turn}"
                : $"Done (550, 306), turn {turn}");
        }
    }
}

/// <summary>
/// The planning clock of one human planning turn (RULE-TIMER-002, RULE-TIMER-003): the limit in
/// milliseconds when the clock started, each redraw of the bar as elapsed milliseconds, width and
/// the effect slot it played (0 for none), the elapsed milliseconds of the last time-limit test
/// that did not end the turn, and of the one that did.
/// </summary>
internal sealed record TimerRecord(int Turn, int LimitMs, List<int[]> Bars, int LastUnexpired, int Expired);

internal sealed record ProbeTrace(
    string Executable,
    NewGameSettings? Settings,
    int Seed,
    List<RollRecord> Rolls,
    int RollsBeforeBegin,
    List<int>? RollsAtDone,
    bool Dumped,
    List<string> Notes,
    List<TimerRecord>? Timers = null);

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
    private bool _planningLoopReached;
    private bool _awardsReached;
    private readonly List<TimerRecord> _timers = [];
    private TimerRecord? _timer;
    private int _turn;

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
        if (settings.ExpireTurns is { Count: > 0 }) ArmTimer();

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

        // Planning has begun when the human's planning loop runs, or, where it does not, when the
        // rolls of the new match have stopped for a while.
        ArmPlanningLoop();
        // A match that ends reaches the endgame instead of another planning phase; the run stops
        // there, once the awards are given (RULE-AWARDS-001).
        _process.SetBreakpoint(OriginalAddresses.AwardsRows, _ => _awardsReached = true, oneShot: true);
        var begun = DateTime.UtcNow;
        var settled = _process.RunUntil(
            () => _rolls.Count > rollsBeforeBegin && PlanningWaits(begun),
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
            foreach (var hire in (settings.Hires ?? []).Where(hire => hire.Turn == turn))
                WriteHire(hire);
            foreach (var write in (settings.Planning ?? []).Where(write => write.Turn == turn))
                WritePlanning(write);
            _rollsAtDone.Add(_rolls.Count);
            _turn = turn;
            var waits = settings.ExpireTurns?.Contains(turn) == true;
            if (waits) _notes.Add($"turn {turn}: no Done press, waiting for the planning time to run out");
            else Click(window, OriginalAddresses.DoneX, OriginalAddresses.DoneY);
            var target = turn;
            var rollsAtClick = _rolls.Count;
            var clicked = DateTime.UtcNow;
            DateTime? moved = null;
            // A press the game did not take leaves the turn where it was with no roll made; press
            // again after a quiet while.
            var next = _process.RunUntil(() =>
            {
                if (_awardsReached) return true;
                // FND-OBJECTIVE-004: a match that ends gives each active human one last look at the
                // city, with the turn's Combat Results open, before the awards controller runs and
                // before elapsed_turns moves on. Close the panels and press Done there.
                if (_process.Read(OriginalAddresses.MatchOver, 1)[0] != 0
                    && DateTime.UtcNow - _process.LastBreakpointUtc > TimeSpan.FromSeconds(2)
                    && DateTime.UtcNow - clicked > TimeSpan.FromSeconds(5))
                {
                    _notes.Add($"the match is over; Done pressed at the final view after roll {_rolls.Count}");
                    ClosePanels(window);
                    Click(window, OriginalAddresses.DoneX, OriginalAddresses.DoneY);
                    clicked = DateTime.UtcNow;
                }
                if (!waits && _rolls.Count == rollsAtClick && _process.ReadInt32(OriginalAddresses.ElapsedTurns) < target
                    && DateTime.UtcNow - clicked > TimeSpan.FromSeconds(20))
                {
                    ClosePanels(window);
                    _notes.Add($"Done of turn {turn} pressed again after roll {_rolls.Count}");
                    Click(window, OriginalAddresses.DoneX, OriginalAddresses.DoneY);
                    clicked = DateTime.UtcNow;
                }
                if (moved is null)
                {
                    if (_process.ReadInt32(OriginalAddresses.ElapsedTurns) < target) return false;
                    // The loop of the turn just ended no longer runs once the count has moved on.
                    moved = DateTime.UtcNow;
                    ArmPlanningLoop();
                }

                return PlanningWaits(moved.Value);
            }, timeout);
            if (!next)
                return Finish(false, $"Turn {turn} never reached the next planning phase (match_over "
                    + $"{_process.Read(OriginalAddresses.MatchOver, 1)[0]}, {_panelsOpen} panel(s) open, elapsed_turns "
                    + $"{_process.ReadInt32(OriginalAddresses.ElapsedTurns)}).", rollsBeforeBegin);
            if (_awardsReached)
            {
                _notes.Add($"The match ended with turn {turn}; the endgame drew the awards after roll {_rolls.Count}.");
                break;
            }
        }

        DumpWritableSections();
        return Finish(true, null, rollsBeforeBegin);
    }

    public void Dispose() => _process.Dispose();

    // FND-TIMER-003: the planning function calls the time-limit test on every pass of the human's
    // planning loop, so its first call after arming means the phase waits for input.
    private void ArmPlanningLoop()
    {
        _planningLoopReached = false;
        _process.SetBreakpoint(OriginalAddresses.PlanningTimeCheck, _ => _planningLoopReached = true, oneShot: true);
    }

    // RULE-TIMER-002, RULE-TIMER-003: the clock of each timed human planning turn, from its start
    // to the time-limit test that ends it. Planning ends on the loop's pass after the limit, so the
    // turn whose Done the probe does not press is recorded from start to expiry.
    private void ArmTimer()
    {
        _process.SetBreakpoint(OriginalAddresses.PlanningTimerStarted, _ =>
        {
            _timer = new TimerRecord(_turn + 1, _process.ReadInt32(OriginalAddresses.PlanningLimitMs), [], -1, -1);
            _lastElapsed = _previousElapsed = -1;
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.PlanningBarWidth, context =>
        {
            if (_timer is null) return;
            var elapsed = _process.ReadInt32(context.Ebp - 4) / 100;
            _timer.Bars.Add([elapsed, _process.ReadInt32(context.Ebp - 8), 0]);
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.PlaySound, context =>
        {
            if (_timer is not { Bars.Count: > 0 } timer) return;
            if (context.ReturnAddress is < OriginalAddresses.PlanningBarDrawStart or > OriginalAddresses.PlanningBarDrawEnd) return;
            timer.Bars[^1][2] = context.Argument(0);
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.PlanningTimeCompare, context =>
        {
            _previousElapsed = _lastElapsed;
            _lastElapsed = (int)context.Eax;
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.PlanningTimeExpired, _ =>
        {
            if (_timer is null) return;
            // The compare saw this pass's elapsed time, which passed the limit, and the pass before
            // it the last elapsed time that did not.
            _timers.Add(_timer with { LastUnexpired = _previousElapsed, Expired = _lastElapsed });
            _notes.Add($"the planning time of turn {_timer.Turn} ran out after roll {_rolls.Count}");
            _timer = null;
        }, quiet: true);
    }

    private int _lastElapsed = -1;
    private int _previousElapsed = -1;

    // The loop's first pass, then half a second for a panel it opens to reach its handler. Several
    // humans stop at a Ready card before the loop, and a loop not seen within 30 seconds falls back
    // to the old test: no roll for 8 seconds.
    private bool PlanningWaits(DateTime since)
    {
        var quiet = DateTime.UtcNow - _process.LastBreakpointUtc;
        if (_planningLoopReached) return quiet > TimeSpan.FromSeconds(0.5);
        return (settings.Humans is { Count: > 1 } || DateTime.UtcNow - since > TimeSpan.FromSeconds(30))
               && quiet > TimeSpan.FromSeconds(8);
    }

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

    private void WriteHire(ProbeHire hire)
    {
        var human = settings.Humans is { Count: > 0 } humans ? humans[0].Slot : 0;
        _process.Write(OriginalAddresses.HireOrders + (uint)(human * 3 + hire.OfferSlot), [(byte)hire.Sector]);
        _notes.Add($"hire after roll {_rolls.Count}: {hire}");
    }

    private void WritePlanning(ProbePlanning write)
    {
        if (write.Family == ProbePlanning.Raider)
            _process.Write(OriginalAddresses.RaiderMode + (uint)write.Player, [1]);
        else
            _process.Write(OriginalAddresses.PlanningRecords
                + (uint)(write.Player * OriginalAddresses.PlanningPlayerStride
                    + write.Slot * OriginalAddresses.PlanningRecordSize), [(byte)write.Family]);
        _notes.Add($"planning after roll {_rolls.Count}: {write}");
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
        if (!settings.Sound) Mute();
        if (settings.EndTurns == 0) return;
        _process.Write(OriginalAddresses.PrefWarnIdle, BitConverter.GetBytes(0));
        _process.Write(OriginalAddresses.PrefDetailedCombat, BitConverter.GetBytes(0));
    }

    // Without --sound the run is silent, as if both volumes of the Options dialog were set to 0
    // (RULE-AUDIO-003): no effect, movie sound or music plays. Nothing the rolls or the state
    // depend on reads these values.
    private void Mute()
    {
        _process.Write(OriginalAddresses.EffectsLevel, BitConverter.GetBytes(0));
        _process.Write(OriginalAddresses.MusicLevel, BitConverter.GetBytes(0));
        _process.Write(OriginalAddresses.EffectsEnabled, [0]);
        _process.Write(OriginalAddresses.MusicEnabled, [0]);
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
        if (settings.TimeLimit is { } limit) _process.Write(OriginalAddresses.PlanningLimitChoice, [(byte)limit]);
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
        return new ProbeTrace(executable, settings, _seed, _rolls, rollsBeforeBegin, _rollsAtDone, dumped, _notes,
            _timers.Count == 0 ? null : _timers);
    }

    private static void Click(IntPtr window, int x, int y)
    {
        var position = (IntPtr)((y << 16) | (x & 0xFFFF));
        Native.PostMessageW(window, Native.WmMouseMove, IntPtr.Zero, position);
        Native.PostMessageW(window, Native.WmLButtonDown, 1, position);
        Native.PostMessageW(window, Native.WmLButtonUp, IntPtr.Zero, position);
    }
}
