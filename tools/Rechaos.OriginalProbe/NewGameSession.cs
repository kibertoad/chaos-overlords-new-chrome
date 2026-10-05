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
/// Search filter entries the probe sets for the first human before the Done press of
/// <paramref name="Turn"/>: a byte of <c>search_filters</c> per site definition, as the Search panel
/// writes them (RULE-SEARCH-001).
/// </summary>
internal sealed record ProbeSearch(int Turn, IReadOnlyList<int> Definitions)
{
    public override string ToString() => $"turn {Turn}: search filter {string.Join(" ", Definitions)}";
}

/// <summary>
/// One city redraw (FND-SEARCH-006): the viewing player and each site marker it drew as
/// definition, sector, ordinal and controlled flag.
/// </summary>
internal sealed record CityMarkers(int Viewer, List<int[]> Markers);

/// A Financial panel the probe opens before the Done press of <paramref name="Turn"/>, after that
/// turn's orders and hires are written: the City variant for sector -1, otherwise the Sector variant
/// of that sector, which the probe selects on the map first (FND-FINANCE-002).
/// </summary>
internal sealed record ProbeFinance(int Turn, int Sector)
{
    public override string ToString() => Sector < 0
        ? $"Financial, City ({OriginalAddresses.FinanceCityX}, {OriginalAddresses.FinanceCityY}), turn {Turn}"
        : $"Financial, Sector ({OriginalAddresses.FinanceSectorX}, {OriginalAddresses.FinanceSectorY}) with sector {Sector} selected, turn {Turn}";
}

/// <summary>
/// 32-bit values the probe writes at <paramref name="Address"/> when the planning-entry function
/// starts to draw the console (FND-UI-040): the first at its first call, the second at its second,
/// and the last at every later call. The console then draws a number the match would not reach,
/// such as a score whose first glyph cell lies outside the glyph sheet (RULE-UI-004), over what an
/// earlier call drew. The write changes the match from then on, so a run that uses it is not
/// replayed. The calls are counted over every human's entries, so the probe takes it with one
/// human only.
/// </summary>
internal sealed record ProbeDrawValue(uint Address, IReadOnlyList<int> Values)
{
    public override string ToString() => $"draw_value 0x{Address:X8} {string.Join("/", Values)}";
}

/// <summary>
/// One call of a planning entry panel (RULE-SETUP-008): Combat Results or Last Turn Events, the
/// roll count when it was called, and whether it stayed open until the probe pressed Exit. The
/// Combat Results function returns at once when no fight qualifies.
/// </summary>
internal sealed record PanelRecord(string Panel, int AfterRoll, bool Shown);

/// <summary>
/// The values one Financial panel drew, in the order it drew them (FND-FINANCE-003), with the sector
/// the probe asked for and the sector the panel function was passed, -2 when it was not called.
/// </summary>
internal sealed record FinanceRecord(int Turn, int Sector, int PanelSector, List<int> Values);

/// <summary>
/// Setup choices the probe writes before Begin; a null leaves what the setup screen opened with.
/// Scenario numbers are the original's (FND-SETUP-013).
/// </summary>
internal sealed record NewGameSettings(
    int? Scenario, int? Mentality, int? TurnLimit, IReadOnlyList<HumanSlot>? Humans, int EndTurns = 0,
    bool TraceHires = false, int? Seed = null, int? DumpAtRoll = null, uint? TraceCalls = null,
    IReadOnlyList<ProbeOrder>? Orders = null, bool Sound = false, IReadOnlyList<ProbeHire>? Hires = null,
    IReadOnlyList<ProbePlanning>? Planning = null, IReadOnlyList<ProbeFinance>? Finance = null,
    IReadOnlyList<ProbeSearch>? Search = null, int? TimeLimit = null, IReadOnlyList<int>? ExpireTurns = null,
    IReadOnlyList<string>? Comlink = null, bool Capture = false, bool WhiteKey = false,
    IReadOnlyList<ProbeDrawValue>? DrawValues = null, bool EquipLists = false, bool AttackLists = false)
{
    public static readonly NewGameSettings Defaults = new(null, null, null, null);

    public IEnumerable<string> Describe()
    {
        if (Scenario is { } scenario) yield return $"scenario {scenario}";
        if (Mentality is { } mentality) yield return $"mentality {mentality}";
        if (TurnLimit is { } turns) yield return $"turn_limit {turns}";
        if (TimeLimit is { } limit) yield return $"planning_limit_choice {limit}";
        if (Comlink is not null) yield return "pref_slide_panels 0";
        if (WhiteKey) yield return "key_colour RGB(255,255,255)";
        foreach (var value in DrawValues ?? []) yield return value.ToString();
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
            var search = (Search ?? []).Where(write => write.Turn == turn).ToArray();
            foreach (var write in search) yield return ("search", write.ToString());
            foreach (var panel in (Finance ?? []).Where(panel => panel.Turn == turn))
                yield return ("left_click", panel.ToString());
            if (ExpireTurns?.Contains(turn) == true)
            {
                yield return ("wait", $"no Done press, turn {turn}: the planning time runs out");
                continue;
            }
            yield return ("left_click", orders.Length + hires.Length + planning.Length + search.Length == 0
                ? $"Done (550, 306) with no orders, turn {turn}"
                : $"Done (550, 306), turn {turn}");
        }
    }
}

/// <summary>
/// The first drawing of the endgame (FND-AWARDS-005): the renderer's three arguments and the player
/// of each name it drew, in drawing order, with the kind of row: splash, ranked or eliminated.
/// </summary>
internal sealed record EndgameDrawing(List<int> Arguments, List<int> Rows, List<string> Kinds);

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
    EndgameDrawing? Endgame = null,
    List<FinanceRecord>? Finance = null,
    List<PanelRecord>? Panels = null,
    CityMarkers? Markers = null,
    List<TimerRecord>? Timers = null,
    List<ComlinkStep>? Comlink = null,
    List<EquipListRecord>? EquipLists = null,
    List<AttackListRecord>? AttackLists = null);

/// <summary>
/// Starts the original in a window, records the seed and every roll, opens a new local game with
/// the given settings, presses Done as many times as asked, and dumps the writable sections once
/// the planning phase that follows waits for the first human.
/// </summary>
internal sealed partial class NewGameSession(
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
    private int _exitPresses;
    private readonly List<PanelRecord> _panels = [];
    private bool _planningLoopReached;
    private bool _awardsReached;
    private EndgameDrawing? _endgame;
    private bool _endgameDrawn;
    private CityMarkers? _redraw;
    private CityMarkers? _lastRedraw;
    private readonly List<FinanceRecord> _finance = [];
    private FinanceRecord? _financeCapture;
    private int _financePanelSector = -2;
    private bool _financeReturned;
    private readonly List<TimerRecord> _timers = [];
    private TimerRecord? _timer;
    private int _turn;
    private int _lastElapsed = -1;
    private int _previousElapsed = -1;

    public ProbeTrace Run()
    {
        _process.SetBreakpoint(OriginalAddresses.SeedGenerator, SeedGenerator);
        _process.SetBreakpoint(OriginalAddresses.Roll, OnRoll);
        _process.SetBreakpoint(OriginalAddresses.PreferenceLoaderCall + 5, ForceWindow, oneShot: true);
        _process.SetBreakpoint(OriginalAddresses.LocalSetup, _ => _setupReached = true);
        if (settings.TraceHires) _process.SetBreakpoint(OriginalAddresses.HireOrderCheck, TraceHire);
        if (settings.TraceCalls is { } traced) _process.SetBreakpoint(traced, TraceCall);
        // FND-PLATFORM-014: on a 32-bit desktop the keyed copies key nothing, so the white the
        // key should drop is drawn. --white-key passes the white a 32-bit surface holds instead.
        // Quiet, because the keyed copies run on every animation tick of a waiting planning phase.
        if (settings.WhiteKey) _process.SetBreakpoint(OriginalAddresses.KeyColourCall, UseThirtyTwoBitKey, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.CombatResults, context => OpenPanel(context, "Combat Results"));
        _process.SetBreakpoint(OriginalAddresses.LastTurnEvents, context => OpenPanel(context, "Last Turn Events"));
        if (settings.Finance is { Count: > 0 })
        {
            _process.SetBreakpoint(OriginalAddresses.FinancePanel, OnFinancePanel);
            _process.SetBreakpoint(OriginalAddresses.NumberDraw, OnNumberDraw);
        }
        if (settings.Search is { Count: > 0 })
        {
            _process.SetBreakpoint(OriginalAddresses.CityRedraw, OnCityRedraw, quiet: true);
            _process.SetBreakpoint(OriginalAddresses.SiteMarker, OnSiteMarker, quiet: true);
        }
        if (settings.ExpireTurns is { Count: > 0 }) ArmTimer();
        if (settings.Comlink is not null) ArmComlink();
        if (settings.DrawValues is { Count: > 0 } drawValues)
        {
            var call = 0;
            _process.SetBreakpoint(OriginalAddresses.PlanningEntryDraw, _ =>
            {
                foreach (var value in drawValues)
                    _process.Write(value.Address, BitConverter.GetBytes(value.Values[Math.Min(call, value.Values.Count - 1)]));
                _notes.Add($"Planning-entry draw {call} after roll {_rolls.Count}.");
                call++;
            });
        }

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
        _process.SetBreakpoint(OriginalAddresses.AwardsRows, OnAwardsRows, oneShot: true);
        var begun = DateTime.UtcNow;
        var settled = _process.RunUntil(
            () => _rolls.Count > rollsBeforeBegin && PlanningWaits(begun),
            timeout);
        if (!settled) return Finish(false, "The new match never settled.", rollsBeforeBegin);

        if (settings.Comlink is not null)
        {
            var failure = RunComlink(window);
            if (failure is not null) return Finish(false, failure, rollsBeforeBegin);
            DumpWritableSections();
            return Finish(true, null, rollsBeforeBegin);
        }

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
            foreach (var write in (settings.Search ?? []).Where(write => write.Turn == turn))
                WriteSearch(write);
            foreach (var panel in (settings.Finance ?? []).Where(panel => panel.Turn == turn))
                if (!CaptureFinance(window, panel))
                    return Finish(false, $"The Financial panel of turn {turn} for sector {panel.Sector} was not captured.", rollsBeforeBegin);
            _rollsAtDone.Add(_rolls.Count);
            _turn = turn;
            var waits = settings.ExpireTurns?.Contains(turn) == true;
            // A turn left to run out takes its planning limit before the resolution even starts, so
            // the wait for the next planning phase gets the limit on top of --timeout.
            var turnTimeout = waits
                ? timeout + TimeSpan.FromMilliseconds(Math.Max(0, _process.ReadInt32(OriginalAddresses.PlanningLimitMs)))
                : timeout;
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
            }, turnTimeout);
            if (!next)
                return Finish(false, $"Turn {turn} never reached the next planning phase (match_over "
                    + $"{_process.Read(OriginalAddresses.MatchOver, 1)[0]}, {_panelsOpen} panel(s) open, elapsed_turns "
                    + $"{_process.ReadInt32(OriginalAddresses.ElapsedTurns)}).", rollsBeforeBegin);
            if (_awardsReached)
            {
                // FND-AWARDS-005: let the renderer's first drawing finish, so every row is kept. A
                // drawing that never finished holds only some of the rows, so none are kept.
                if (!_process.RunUntil(() => _endgameDrawn, TimeSpan.FromSeconds(10)))
                {
                    _endgame = null;
                    _notes.Add("The endgame renderer did not return within 10 seconds; its rows are not kept.");
                }
                _notes.Add($"The match ended with turn {turn}; the endgame drew the awards after roll {_rolls.Count}.");
                break;
            }
        }

        DumpWritableSections();
        if (settings.Capture) CaptureDrawingArea(window);
        if (settings.EquipLists && !RecordEquipLists()) return Finish(false, "The Equip lists were not built.", rollsBeforeBegin);
        if (settings.AttackLists && !RecordAttackLists()) return Finish(false, "The Attack lists were not built.", rollsBeforeBegin);
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
        var presses = _exitPresses;
        // Kept in the order of the calls. A panel still open when the run ends was shown at the
        // last planning entry, so it stays marked shown until its handler returns.
        var index = _panels.Count;
        _panels.Add(new PanelRecord(panel, _rolls.Count, true));
        _process.SetBreakpoint(context.ReturnAddress, _ =>
        {
            _panelsOpen--;
            _panels[index] = _panels[index] with { Shown = _exitPresses > presses };
        }, oneShot: true);
    }

    // Presses Exit until every panel handler that opened has returned.
    private bool ClosePanels(IntPtr window)
    {
        for (var attempt = 0; _panelsOpen > 0 && attempt < 10; attempt++)
        {
            _exitPresses++;
            Click(window, OriginalAddresses.PanelExitX, OriginalAddresses.PanelExitY);
            _process.RunUntil(() => _panelsOpen == 0, TimeSpan.FromSeconds(3));
        }

        return _panelsOpen == 0;
    }

    // FND-AWARDS-005: the renderer's first call, kept until it returns.
    private void OnAwardsRows(BreakContext context)
    {
        _awardsReached = true;
        _endgame = new EndgameDrawing([context.Argument(0), context.Argument(1), context.Argument(2)], [], []);
        // Set only now: the helper draws every text of the game.
        _process.SetBreakpoint(OriginalAddresses.TextDraw, OnTextDraw);
        _process.SetBreakpoint(context.ReturnAddress, _ => _endgameDrawn = true, oneShot: true);
    }

    private void OnTextDraw(BreakContext context)
    {
        if (_endgameDrawn || _endgame is not { } endgame) return;
        var kind = (context.ReturnAddress - 5) switch
        {
            OriginalAddresses.SplashNameDraw => "splash",
            OriginalAddresses.RankedNameDraw => "ranked",
            OriginalAddresses.EliminatedNameDraw => "eliminated",
            _ => null,
        };
        if (kind is null) return;
        endgame.Rows.Add((int)(((uint)context.Argument(2) - OriginalAddresses.PlayerNames) / OriginalAddresses.PlayerNameStride));
        endgame.Kinds.Add(kind);
    }

    // The player whose orders, hires, Search filter, Equip lists and Attack lists the probe writes and reads:
    // the first --humans entry, or slot 0 when the option is left out.
    private int FirstHuman => settings.Humans is { Count: > 0 } humans ? humans[0].Slot : 0;

    private void WriteSearch(ProbeSearch write)
    {
        var human = FirstHuman;
        foreach (var definition in write.Definitions)
            _process.Write(OriginalAddresses.SearchFilters
                + (uint)(human * OriginalAddresses.SiteDefinitionCount + definition), [1]);
        _notes.Add($"search after roll {_rolls.Count}: {write}");
    }

    // FND-SEARCH-006: each city redraw's markers, kept once the redraw returns; the dump keeps the
    // last complete redraw.
    private void OnCityRedraw(BreakContext context)
    {
        var redraw = new CityMarkers(context.Argument(0), []);
        _redraw = redraw;
        _process.SetBreakpoint(context.ReturnAddress, _ =>
        {
            if (_redraw == redraw) _lastRedraw = redraw;
            _redraw = null;
        }, oneShot: true);
    }

    private void OnSiteMarker(BreakContext context) =>
        _redraw?.Markers.Add([context.Argument(0), context.Argument(1), context.Argument(2), context.Argument(3) & 0xFF]);

    // FND-FINANCE-002, FND-FINANCE-003: selects the sector for the Sector variant, presses the part
    // of the Financial control that opens the variant, keeps the nine numbers the panel draws, and
    // presses its close control until the panel function has returned. A capture counts only when
    // the panel function was passed the asked sector, -1 for the City variant, so a press that opens
    // the other variant cannot be recorded as this one.
    private bool CaptureFinance(IntPtr window, ProbeFinance panel)
    {
        if (panel.Sector >= 0) _process.Write(OriginalAddresses.SelectedSector, BitConverter.GetBytes(panel.Sector));
        var capture = new FinanceRecord(panel.Turn, panel.Sector, -2, []);
        _financePanelSector = -2;
        _financeCapture = capture;
        _financeReturned = false;
        if (panel.Sector < 0) Click(window, OriginalAddresses.FinanceCityX, OriginalAddresses.FinanceCityY);
        else Click(window, OriginalAddresses.FinanceSectorX, OriginalAddresses.FinanceSectorY);
        var drawn = _process.RunUntil(() => capture.Values.Count == 9, TimeSpan.FromSeconds(10));
        _process.Pump(TimeSpan.FromSeconds(0.5));
        for (var attempt = 0; !_financeReturned && attempt < 10; attempt++)
        {
            Click(window, OriginalAddresses.FinanceCloseX, OriginalAddresses.FinanceCloseY);
            _process.RunUntil(() => _financeReturned, TimeSpan.FromSeconds(3));
        }

        _financeCapture = null;
        _finance.Add(capture with { PanelSector = _financePanelSector });
        _notes.Add($"Financial panel of turn {panel.Turn} for sector {panel.Sector} opened for sector {_financePanelSector} and drew [{string.Join(",", capture.Values)}]");
        return drawn && _financeReturned && _financePanelSector == panel.Sector;
    }

    private void OnFinancePanel(BreakContext context)
    {
        if (_financeCapture is null) return;
        _financePanelSector = context.Argument(1);
        _process.SetBreakpoint(context.ReturnAddress, _ => _financeReturned = true, oneShot: true);
    }

    // FND-FINANCE-003: only the panel's own draws in fn_0044D1BB lie in the checked range, so a draw
    // there is the open panel's.
    private void OnNumberDraw(BreakContext context)
    {
        if (_financeCapture is not { } capture || capture.Values.Count == 9) return;
        var call = context.ReturnAddress - 5;
        if (call < OriginalAddresses.FinanceFirstDraw || call > OriginalAddresses.FinanceLastDraw) return;
        capture.Values.Add(context.Argument(2));
    }

    private void WriteOrder(ProbeOrder order)
    {
        var human = FirstHuman;
        var record = OriginalAddresses.GangRecords
            + (uint)(human * OriginalAddresses.PlayerGangStride + order.Slot * OriginalAddresses.GangRecordSize);
        _process.Write(record + 7, [
            (byte)order.Action, (byte)order.Target, (byte)order.Target2,
            (byte)(order.Repeat ? order.Action : 0), (byte)(order.Repeat ? order.Target : 0)]);
        _notes.Add($"order after roll {_rolls.Count}: {order}");
    }

    private void WriteHire(ProbeHire hire)
    {
        var human = FirstHuman;
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

    private void UseThirtyTwoBitKey(BreakContext context)
    {
        // At the call instruction the device context is at [esp] and the colour at [esp + 4].
        if (_process.ReadInt32(context.Esp + 4) == OriginalAddresses.SixteenBitWhiteKey)
            _process.Write(context.Esp + 4, BitConverter.GetBytes(OriginalAddresses.ThirtyTwoBitWhite));
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
        if (settings.Comlink is not null) _process.Write(OriginalAddresses.PrefSlidePanels, BitConverter.GetBytes(0));
        if (settings.EndTurns == 0 && settings.Comlink is null) return;
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

    // RULE-GFX-002: the 640-by-460 drawing area starts at the client area's top-left corner. The
    // capture is written twice from the window's device context. PrintWindow is unsuitable here:
    // it can repaint over animation drawn directly to the window rather than its backing surface.
    private void CaptureDrawingArea(IntPtr window)
    {
        const int width = 640, height = 460;
        // A smaller client area leaves part of the copy outside the window, and that part is not
        // the original's drawing.
        if (!Native.GetClientRect(window, out var client)
            || client.Right - client.Left < width || client.Bottom - client.Top < height)
        {
            _notes.Add($"Capture rejected: the client area is {client.Right - client.Left} by "
                + $"{client.Bottom - client.Top}, smaller than the {width}-by-{height} drawing area.");
            return;
        }
        _notes.Add($"Client area {client.Right - client.Left} by {client.Bottom - client.Top}.");
        // FND-UI-038: the counter increments after drawing. Require two agreeing window copies
        // and a stable counter; a repainting capture cannot use this frame relationship. The
        // pump's counter, which picks the selected-sector frame and the lights' blink phase
        // (FND-UI-017, FND-EVENT-006), is kept as read; which of its values a capture shows is
        // not recorded yet.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var before = BitConverter.ToInt16(_process.Read(OriginalAddresses.MarkerCounter, 2));
            var pumpBefore = _process.ReadInt32(OriginalAddresses.PumpCounter);
            var copiesAgree = CaptureDrawingArea(window, width, height);
            var after = BitConverter.ToInt16(_process.Read(OriginalAddresses.MarkerCounter, 2));
            var pumpAfter = _process.ReadInt32(OriginalAddresses.PumpCounter);
            if (before != after || pumpBefore != pumpAfter || !copiesAgree) continue;
            _notes.Add($"marker_frame {(before + 11) % 12}");
            _notes.Add($"pump_counter {pumpBefore}");
            return;
        }
        _notes.Add("Capture rejected: a counter moved or the synchronized copies disagreed.");
    }

    private bool CaptureDrawingArea(IntPtr window, int width, int height)
    {
        byte[]? firstCopy = null;
        var copiesAgree = false;
        foreach (var name in new[] { "capture-blt.bmp", "capture-blt-repeat.bmp" })
        {
            var info = new byte[40];
            BitConverter.GetBytes(40).CopyTo(info, 0);
            BitConverter.GetBytes(width).CopyTo(info, 4);
            BitConverter.GetBytes(-height).CopyTo(info, 8);
            BitConverter.GetBytes((short)1).CopyTo(info, 12);
            BitConverter.GetBytes((short)32).CopyTo(info, 14);
            var screen = Native.GetDC(window);
            var memory = IntPtr.Zero;
            var bitmap = IntPtr.Zero;
            var old = IntPtr.Zero;
            try
            {
                if (screen == IntPtr.Zero) throw new InvalidOperationException("Cannot acquire the capture window DC.");
                if (firstCopy is null)
                {
                    const int bitsPixel = 12, planes = 14;
                    var hostDepth = Native.GetDeviceCaps(screen, bitsPixel) * Native.GetDeviceCaps(screen, planes);
                    var gameDepth = _process.ReadInt32(OriginalAddresses.DisplayDepth);
                    _notes.Add($"Capture depths: original records {gameDepth}; probe window DC reports {hostDepth}.");
                    if (gameDepth != hostDepth)
                        _notes.Add(settings.WhiteKey
                            ? "Capture depth mismatch: --white-key passed RGB(255,255,255) for the 16-bit key (FND-PLATFORM-014)."
                            : "Capture depth mismatch: evaluate colour-key conversion before accepting presentation evidence.");
                }
                memory = Native.CreateCompatibleDC(screen);
                if (memory == IntPtr.Zero) throw new InvalidOperationException("Cannot create the capture memory DC.");
                bitmap = Native.CreateDIBSection(screen, info, 0, out var bits, IntPtr.Zero, 0);
                if (bitmap == IntPtr.Zero || bits == IntPtr.Zero)
                    throw new InvalidOperationException("Cannot allocate the capture bitmap.");
                old = Native.SelectObject(memory, bitmap);
                if (old == IntPtr.Zero || old == new IntPtr(-1))
                    throw new InvalidOperationException("Cannot select the capture bitmap.");
                var ok = Native.BitBlt(memory, 0, 0, width, height, screen, 0, 0, 0x00CC0020);
                if (!ok) throw new InvalidOperationException($"{name}: the copy failed.");
                // CreateDIBSection requires GDI drawing to finish before its bits are read directly.
                // https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection
                if (!Native.GdiFlush()) throw new InvalidOperationException($"{name}: flushing the copy failed.");
                var pixels = new byte[width * height * 4];
                System.Runtime.InteropServices.Marshal.Copy(bits, pixels, 0, pixels.Length);
                if (firstCopy is null) firstCopy = pixels;
                else copiesAgree = firstCopy.AsSpan().SequenceEqual(pixels);
                WriteBitmap(Path.Combine(outputDirectory, name), width, height, pixels);
            }
            finally
            {
                if (old != IntPtr.Zero && old != new IntPtr(-1)) Native.SelectObject(memory, old);
                if (bitmap != IntPtr.Zero) Native.DeleteObject(bitmap);
                if (memory != IntPtr.Zero) Native.DeleteDC(memory);
                if (screen != IntPtr.Zero) Native.ReleaseDC(window, screen);
            }
        }
        return copiesAgree;
    }

    private static void WriteBitmap(string path, int width, int height, byte[] topDownBgra)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B'); writer.Write((byte)'M');
        writer.Write(54 + topDownBgra.Length); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(width); writer.Write(-height);
        writer.Write((short)1); writer.Write((short)32); writer.Write(0);
        writer.Write(topDownBgra.Length); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        writer.Write(topDownBgra);
    }

    private ProbeTrace Finish(bool dumped, string? note, int rollsBeforeBegin = 0)
    {
        if (note is not null) _notes.Add(note);
        _notes.AddRange(_process.Log);
        if (_process.Exited) _notes.Add($"The process exited with code 0x{_process.ExitCode:X8}.");
        return new ProbeTrace(executable, settings, _seed, _rolls, rollsBeforeBegin, _rollsAtDone, dumped, _notes,
            _endgame, _finance.Count == 0 ? null : _finance, _panels.Count == 0 ? null : _panels, _lastRedraw,
            _timers.Count == 0 ? null : _timers, _comlink.Count == 0 ? null : _comlink,
            _equipLists.Count == 0 ? null : _equipLists, _attackLists.Count == 0 ? null : _attackLists);
    }

    private static void Click(IntPtr window, int x, int y)
    {
        var position = (IntPtr)((y << 16) | (x & 0xFFFF));
        Native.PostMessageW(window, Native.WmMouseMove, IntPtr.Zero, position);
        Native.PostMessageW(window, Native.WmLButtonDown, 1, position);
        Native.PostMessageW(window, Native.WmLButtonUp, IntPtr.Zero, position);
    }
}
