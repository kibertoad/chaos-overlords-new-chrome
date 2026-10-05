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
/// A player's state written before a Done press, for branches no local match reaches: family 99
/// or less writes the <c>family</c> of a computer player's planning record in the slot
/// (FMT-STATE-007), <see cref="Raider"/> sets a computer player's byte of <c>raider_mode</c>, which
/// a takeover of a network seat sets (RULE-AI-027), and <see cref="Retired"/> clears the player's
/// byte of <c>player_active</c>, as the elimination check does (RULE-TURN-006).
/// </summary>
internal sealed record ProbePlanning(int Turn, int Player, int Slot, int Family)
{
    public const int Raider = -1;
    public const int Retired = -2;

    public override string ToString() => Family switch
    {
        Raider => $"turn {Turn}: player {Player} raider_mode 1",
        Retired => $"turn {Turn}: player {Player} player_active 0",
        _ => $"turn {Turn}: player {Player} gang slot {Slot} family {Family}",
    };
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
/// A left-button press and release the probe posts at a client point once the dump is taken
/// (<c>--search-clicks</c>, SearchClickRecord).
/// </summary>
internal sealed record ProbeClick(int X, int Y)
{
    public override string ToString() => $"({X}, {Y}) after the dump";
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
    IReadOnlyList<ProbeDrawValue>? DrawValues = null, bool EquipLists = false, bool AttackLists = false,
    IReadOnlyList<ProbeClick>? SearchClicks = null, IReadOnlyList<ProbeHireStep>? HireSteps = null,
    IReadOnlyList<ProbeOrderStep>? OrderSteps = null, bool GangMarkers = false, bool TitleCapture = false,
    bool CreditsCapture = false, bool SetupCapture = false, IReadOnlyList<ProbeOrderStep>? SetupSteps = null,
    bool DetailedCombat = false, bool Pointer = false, bool Sounds = false)
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
        foreach (var human in Humans ?? [])
            yield return human.Modifier is null
                ? $"slot {human.Slot}: human"
                : $"slot {human.Slot}: human named modifier_name_{human.Modifier}";
        // The presses on the setup screen come before the settings are written, but they are kept
        // with them so that runs with other presses are told apart and the fixture lists them.
        for (var index = 0; index < (SetupSteps?.Count ?? 0); index++)
            yield return $"setup step {index}: {DescribeSetupStep(SetupSteps![index])}";
    }

    private static string DescribeSetupStep(ProbeOrderStep step) => step.Kind switch
    {
        "strip" => $"press ({step.X}, {step.Y})",
        "drag" => $"drag ({step.X}, {step.Y}) to ({step.Target}, {step.Choice})",
        _ => $"capture for {step.Screens}",
    };

    /// <summary>
    /// The orders and Done presses after the first planning phase, then the presses after the dump,
    /// one input each: an order is named <c>order</c>, a press <c>left_click</c>, a hire step's
    /// drag <c>drag</c> and a wait <c>wait</c>.
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
        foreach (var click in SearchClicks ?? []) yield return ("left_click", click.ToString());
        foreach (var step in HireSteps ?? [])
            yield return (step.Slot >= 0 && step.Sector != -2 ? "drag" : "left_click", $"{step} after the dump");
        foreach (var step in OrderSteps ?? [])
            yield return (step.Kind switch { "open" => "double_click", "wait" => "wait", "type" => "key", _ => "left_click" },
                $"{step} after the dump");
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
    List<AttackListRecord>? AttackLists = null,
    List<SearchClickRecord>? SearchClicks = null,
    List<HireStepRecord>? HireSteps = null,
    List<OrderStepRecord>? OrderSteps = null,
    List<GangMarkerDraw>? GangMarkers = null,
    List<CombatClipRecord>? CombatClips = null,
    List<CombatPresentationRecord>? CombatPresentations = null,
    List<PointerCallRecord>? PointerCalls = null,
    List<SoundCallRecord>? SoundCalls = null);

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
    // The panel calls as they stood at the dump: presses made after it, such as a hire step's
    // Exit, close panels and open others that belong to no planning entry of the run.
    private List<PanelRecord>? _panelsAtDump;
    private bool _planningLoopReached;
    private bool _awardsReached;
    private bool _eliminationCardReached;
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
        if (settings.GangMarkers) ArmGangMarkers();
        if (settings.ExpireTurns is { Count: > 0 }) ArmTimer();
        if (settings.Comlink is not null) ArmComlink();
        if (settings.DetailedCombat) ArmDetailedCombat();
        if (settings.Pointer) ArmPointer();
        if (settings.Sounds) ArmSounds();
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
        // --title-capture, --credits-capture and --setup-capture: New Game waits until the title has
        // drawn its art (FND-UI-055) and the drawing area has been copied, and for the credits until
        // About has shown them (FND-UI-007) and they have been copied and closed.
        var nextPoke = DateTime.MinValue;
        if (settings.TitleCapture || settings.CreditsCapture || settings.SetupCapture)
        {
            var titleShown = false;
            _process.SetBreakpoint(OriginalAddresses.TitleArtLoaded, _ => titleShown = true, oneShot: true);
            var titleReached = _process.RunUntil(() =>
            {
                if (titleShown) return true;
                if (DateTime.UtcNow < nextPoke) return false;
                nextPoke = DateTime.UtcNow.AddSeconds(0.5);
                _process.Write(OriginalAddresses.LeftButtonDown, [1]);
                return false;
            }, timeout);
            _process.Write(OriginalAddresses.LeftButtonDown, [0]);
            if (!titleReached) return Finish(false, "The title art was never loaded.");
            _process.Pump(TimeSpan.FromSeconds(2));
            if (settings.TitleCapture) CaptureBeforeMatch(window, "title");
            if (settings.CreditsCapture) CaptureCredits(window);
            if (settings.SetupCapture)
            {
                // FND-OPTIONS-001: the objective, Mentality and planning limit take their
                // initialized values, as when the registry key holds none, so setup opens with them.
                _process.Write(OriginalAddresses.PreferredScenario, BitConverter.GetBytes(0));
                _process.Write(OriginalAddresses.Mentality, BitConverter.GetBytes(1));
                _process.Write(OriginalAddresses.PlanningLimitChoice, BitConverter.GetBytes(0));
            }
        }
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
        // --setup-capture: the setup screen as New Game opened it, before the settings are written.
        if (settings.SetupCapture) CaptureBeforeMatch(window, "setup");
        var choicesBeforeSteps = SetupChoicesLeftToPresses();
        RecordSetupSteps(window, settings.SetupSteps);
        if (SetupStepsNote(choicesBeforeSteps) is { } setupStepsNote) _notes.Add(setupStepsNote);
        var rollsBeforeBegin = _rolls.Count;
        ApplySettings();
        Click(window, OriginalAddresses.BeginX, OriginalAddresses.BeginY);

        // Planning has begun when the human's planning loop runs, or, where it does not, when the
        // rolls of the new match have stopped for a while.
        ArmPlanningLoop();
        // A match that ends reaches the endgame instead of another planning phase; the run stops
        // there, once the awards are given (RULE-AWARDS-001).
        _process.SetBreakpoint(OriginalAddresses.AwardsRows, OnAwardsRows, oneShot: true);
        // RULE-OBJECTIVE-005: the human's elimination reaches its card instead; the run stops there.
        _process.SetBreakpoint(OriginalAddresses.EliminationCard, _ => _eliminationCardReached = true, oneShot: true);
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
                if (_awardsReached || _eliminationCardReached) return true;
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
            if (_eliminationCardReached)
            {
                // The card draws once and then waits for its Done.
                _process.Pump(TimeSpan.FromSeconds(1.5));
                _notes.Add($"The human was eliminated with turn {turn}; its card opened after roll {_rolls.Count}.");
                break;
            }
        }

        DumpWritableSections();
        _gangMarkersDumped = true;
        _panelsAtDump = [.. _panels];
        // RULE-SETUP-008: steps after the dump can call roll, as a Ready press refills the offers.
        _notes.Add($"rolls_at_dump {_rolls.Count}");
        if (settings.Capture && CaptureDrawingArea(window, "capture-blt") is var (marker, pump, lamps, selected))
        {
            _notes.Add($"marker_frame {marker}");
            _notes.Add($"pump_counter {pump}");
            _notes.Add($"lamps {string.Join(' ', lamps)}");
            _notes.Add($"selected_sector {selected}");
        }
        if (settings.EquipLists && !RecordEquipLists()) return Finish(false, "The Equip lists were not built.", rollsBeforeBegin);
        if (settings.AttackLists && !RecordAttackLists()) return Finish(false, "The Attack lists were not built.", rollsBeforeBegin);
        if (settings.SearchClicks is { Count: > 0 } && !RecordSearchClicks(window))
            return Finish(false, "The original exited during the Search clicks.", rollsBeforeBegin);
        if (settings.HireSteps is { Count: > 0 } && RecordHireSteps(window) is { } stopped)
            return Finish(false, stopped, rollsBeforeBegin);
        if (settings.OrderSteps is { Count: > 0 } && RecordOrderSteps(window) is { } orderStepsStopped)
            return Finish(false, orderStepsStopped, rollsBeforeBegin);
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
        if (_detailedCombatOpen) return false;
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

    // An Exit press of a step after the dump. With no panel open the Exit point lies on the city
    // map, where a press would select a sector and a second one open the sector view, so the
    // press is skipped. A press that closes a panel counts as for ClosePanels, so the panel is
    // recorded as shown.
    private void PressExitAfterDump(IntPtr window)
    {
        if (_panelsOpen == 0)
        {
            _notes.Add("exit after the dump skipped: no panel was open");
            return;
        }
        _exitPresses++;
        Click(window, OriginalAddresses.PanelExitX, OriginalAddresses.PanelExitY);
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
        else if (write.Family == ProbePlanning.Retired)
            _process.Write(OriginalAddresses.PlayerActive + (uint)write.Player, [0]);
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
        _process.Write(OriginalAddresses.PrefDetailedCombat, BitConverter.GetBytes(settings.DetailedCombat ? 1 : 0));
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
            _endgame, _finance.Count == 0 ? null : _finance, (_panelsAtDump ?? _panels) is { Count: > 0 } panels ? panels : null, _lastRedraw,
            _timers.Count == 0 ? null : _timers, _comlink.Count == 0 ? null : _comlink,
            _equipLists.Count == 0 ? null : _equipLists, _attackLists.Count == 0 ? null : _attackLists,
            _searchClicks.Count == 0 ? null : _searchClicks, _hireSteps.Count == 0 ? null : _hireSteps,
            _orderSteps.Count == 0 ? null : _orderSteps, settings.GangMarkers ? _gangMarkers : null,
            settings.DetailedCombat ? _combatClips : null, settings.DetailedCombat ? _combatPresentations : null,
            settings.Pointer ? _pointerCalls : null, settings.Sounds ? _soundCalls : null);
    }

    private static void Click(IntPtr window, int x, int y)
    {
        var position = PointParameter(x, y);
        Native.PostMessageW(window, Native.WmMouseMove, IntPtr.Zero, position);
        Native.PostMessageW(window, Native.WmLButtonDown, 1, position);
        Native.PostMessageW(window, Native.WmLButtonUp, IntPtr.Zero, position);
    }

    // A client point as the mouse messages carry it in lParam, and as the window procedure keeps
    // it: x in the low 16 bits, y in the high 16 (FND-UI-020).
    private static IntPtr PointParameter(int x, int y) => (IntPtr)((y << 16) | (x & 0xFFFF));
}
