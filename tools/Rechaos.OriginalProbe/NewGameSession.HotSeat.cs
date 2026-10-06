namespace Rechaos.OriginalProbe;

/// <summary>A wait of <paramref name="Milliseconds"/> before the Done press of <paramref name="Turn"/>.</summary>
internal sealed record ProbeDelay(int Turn, int Milliseconds)
{
    public override string ToString() => $"{Milliseconds} ms before the Done press, turn {Turn}";
}

/// <summary>
/// The menu bar held open during the first human's planning of <paramref name="Turn"/>: opened once
/// the planning clock shows <paramref name="AfterMs"/> elapsed milliseconds, and closed
/// <paramref name="HoldMs"/> milliseconds later (SCR-UI-009).
/// </summary>
internal sealed record ProbeMenu(int Turn, int AfterMs, int HoldMs)
{
    public override string ToString() =>
        $"menu bar opened (SC_KEYMENU) at {AfterMs} ms of the planning clock and closed (Escape) {HoldMs} ms later, turn {Turn}";
}

/// <summary>
/// One holding of the menu bar: the turn, the elapsed milliseconds of the planning clock when the
/// probe posted the opening keys, when the window's thread was seen in menu mode, when the probe
/// posted Escape and when the thread had left menu mode, the GUITHREADINFO flags seen while it
/// was open, and each tick of timer slot 0, the presentation clock (FND-UI-023), from the clock's
/// start to the expiry of the turn.
/// </summary>
internal sealed record MenuRecord(int Turn, int PostedMs, int OpenMs, int ClosingMs, int ClosedMs, uint Flags, List<int> Ticks);

/// <summary>
/// A copy of the drawing area taken at the start of a human's planning clock, before the start
/// helper draws the bar (FND-TIMER-003): the player, <c>elapsed_turns</c>, the bitmap in the run
/// directory, and the width and elapsed milliseconds of the last bar drawn before it, -1 for none.
/// </summary>
internal sealed record ClockCaptureRecord(int Player, int ElapsedTurns, string File, int LastWidth, int LastElapsed);

// The local hot-seat match (RULE-SETUP-008, RULE-OBJECTIVE-005): hand-off cards, the later humans'
// turns, elimination cards, the menu bar and the planning clock's start.
internal sealed partial class NewGameSession
{
    private bool _eliminationCardOpen;
    private int _eliminationCardPlayer = -1;
    private readonly List<int> _eliminationCards = [];
    private readonly List<MenuRecord> _menus = [];
    private readonly List<ClockCaptureRecord> _clockCaptures = [];
    private int _lastBarWidth = -1;
    private List<int>? _turnTicks;

    // The ticks of timer slot 0 through the timer callback fn_004327C0, whose third argument is the
    // slot (FND-TIMER-002), in elapsed milliseconds of the planning clock, from the start of the
    // clock of a turn with a --menu hold to its expiry.
    private void ArmMenuTicks()
    {
        _process.SetBreakpoint(OriginalAddresses.PlanningTimerStarted, _ =>
            _turnTicks = settings.Menus!.Any(menu => menu.Turn == _turn + 1) ? [] : null, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.TimerCallback, context =>
        {
            if (_turnTicks is { } ticks && context.Argument(2) == 0)
                ticks.Add(unchecked((int)(Native.timeGetTime() - (uint)_process.ReadInt32(OriginalAddresses.PlanningStartMs))));
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.PlanningTimeExpired, _ => _turnTicks = null, quiet: true);
    }
    private int _lastBarElapsed = -1;

    // FND-SETUP-016: the hand-off card's presenter takes the next player's slot and returns once
    // its Ready has been released.
    private void ArmHandoffCard()
    {
        _process.SetBreakpoint(OriginalAddresses.HandoffCard, context =>
        {
            _cardPlayer = context.Argument(0);
            _cardOpen = true;
            _process.SetBreakpoint(context.ReturnAddress, _ => _cardOpen = false, oneShot: true);
        });
    }

    // FND-OBJECTIVE-002, RULE-OBJECTIVE-005: the elimination card blocks until its Done is
    // released. The run stops at the first card it reaches, unless --pass-cards lets it press
    // the card's Done and go on.
    private void ArmEliminationCard()
    {
        _process.SetBreakpoint(OriginalAddresses.EliminationCard, context =>
        {
            _eliminationCardOpen = true;
            _eliminationCardPlayer = _process.ReadInt32(OriginalAddresses.ActivePlayer);
            if (!settings.PassCards) _eliminationCardReached = true;
            _process.SetBreakpoint(context.ReturnAddress, _ => _eliminationCardOpen = false, oneShot: true);
        });
    }

    // Presses the card's Done until the presenter has returned.
    private bool PassEliminationCard(IntPtr window)
    {
        // The card draws once and then waits for its Done.
        _process.Pump(TimeSpan.FromSeconds(1.5));
        var player = _eliminationCardPlayer;
        for (var attempt = 0; _eliminationCardOpen && attempt < 10; attempt++)
        {
            Click(window, OriginalAddresses.CardDoneX, OriginalAddresses.CardDoneY);
            _process.RunUntil(() => !_eliminationCardOpen, TimeSpan.FromSeconds(3));
        }
        if (_eliminationCardOpen) return false;
        _eliminationCards.Add(player);
        _notes.Add($"elimination card with active_player {player} passed after roll {_rolls.Count}");
        return true;
    }

    // RULE-SETUP-008: presses Ready on the hand-off card of the player, then Exit on each planning
    // entry panel, and returns once the planning loop runs, or, in the final view of a match that
    // is over, once the game has been quiet for three seconds. Returns why it failed, or null.
    private string? HandOff(IntPtr window, int player)
    {
        if (!_cardOpen) return $"No hand-off card was open for player {player}.";
        if (_cardPlayer != player) return $"The hand-off card is for player {_cardPlayer}, not {player}.";
        ArmPlanningLoop();
        for (var attempt = 0; _cardOpen && attempt < 10; attempt++)
        {
            Click(window, OriginalAddresses.ReadyX, OriginalAddresses.ReadyY);
            _process.RunUntil(() => !_cardOpen, TimeSpan.FromSeconds(3));
        }
        if (_cardOpen) return $"The hand-off card for player {player} never closed.";
        var since = DateTime.UtcNow;
        while (true)
        {
            var waited = _process.RunUntil(() =>
            {
                var quiet = DateTime.UtcNow - _process.LastBreakpointUtc;
                return _eliminationCardOpen
                    || (_panelsOpen > 0 && quiet > TimeSpan.FromSeconds(0.5))
                    || (_planningLoopReached && _panelsOpen == 0 && quiet > TimeSpan.FromSeconds(0.5))
                    || (_process.Read(OriginalAddresses.MatchOver, 1)[0] != 0 && _panelsOpen == 0
                        && quiet > TimeSpan.FromSeconds(3));
            }, timeout);
            if (!waited) return $"Player {player}'s planning never came after Ready.";
            if (_eliminationCardOpen) return null;
            if (_panelsOpen > 0)
            {
                if (!ClosePanels(window)) return $"A planning entry panel of player {player} never closed.";
                continue;
            }
            var active = _process.ReadInt32(OriginalAddresses.ActivePlayer);
            _notes.Add($"player {player} planning after Ready, active_player {active}, after roll {_rolls.Count}, {(DateTime.UtcNow - since).TotalSeconds:F1} s");
            return active == player ? null : $"Planning is player {active}'s, not {player}'s.";
        }
    }

    // --menu: once the planning clock shows AfterMs elapsed, posts SC_KEYMENU, as Alt does, which
    // puts the menu bar in menu mode, and Escape HoldMs later. The clock's start is the
    // planning_start_ms that the start helper stores (FND-TIMER-003), and timeGetTime is the clock
    // it was stored from. Returns why it failed, or null.
    private string? HoldMenu(IntPtr window, ProbeMenu menu)
    {
        var threadId = Native.GetWindowThreadProcessId(window, out _);
        var start = (uint)_process.ReadInt32(OriginalAddresses.PlanningStartMs);
        int Elapsed() => unchecked((int)(Native.timeGetTime() - start));
        uint Flags()
        {
            var info = new Native.GuiThreadInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.GuiThreadInfo>() };
            return Native.GetGUIThreadInfo(threadId, ref info) ? info.Flags : 0;
        }
        bool InMenu() => (Flags() & (Native.GuiInMenuMode | Native.GuiPopupMenuMode)) != 0;

        if (Elapsed() < menu.AfterMs) _process.Pump(TimeSpan.FromMilliseconds(menu.AfterMs - Elapsed()));
        var posted = Elapsed();
        Native.PostMessageW(window, Native.WmSysCommand, Native.ScKeyMenu, IntPtr.Zero);
        if (!_process.RunUntil(InMenu, TimeSpan.FromSeconds(3))) return "The menu bar did not open.";
        var open = Elapsed();
        var flags = Flags();
        var until = posted + menu.HoldMs;
        // Menu mode ends early when the window loses the activation, which a run must not count
        // as a held menu.
        _process.RunUntil(() => !InMenu() || Elapsed() >= until, TimeSpan.FromMilliseconds(Math.Max(0, until - Elapsed()) + 1000));
        if (!InMenu()) return $"The menu bar left menu mode on its own at {Elapsed()} ms of the planning clock.";
        var closing = Elapsed();
        for (var attempt = 0; InMenu() && attempt < 10; attempt++)
        {
            Native.PostMessageW(window, Native.WmKeyDown, Native.VkEscape, 1);
            Native.PostMessageW(window, Native.WmKeyUp, Native.VkEscape, unchecked((IntPtr)0xC0000001));
            _process.RunUntil(() => !InMenu(), TimeSpan.FromSeconds(1));
        }
        if (InMenu()) return "The menu bar did not close.";
        var closed = Elapsed();
        _menus.Add(new MenuRecord(menu.Turn, posted, open, closing, closed, flags, _turnTicks ?? []));
        _notes.Add($"menu bar of turn {menu.Turn}: posted {posted} ms, open {open} ms (flags 0x{flags:X}), Escape {closing} ms, closed {closed} ms");
        return null;
    }

    // --clock-captures: a copy of the drawing area at each start of a human's planning clock,
    // before the start helper draws the bar, with the last bar drawn before it (FND-TIMER-003).
    private void ArmClockCaptures(IntPtr window)
    {
        _process.SetBreakpoint(OriginalAddresses.PlanningBarWidth, context =>
        {
            _lastBarElapsed = _process.ReadInt32(context.Ebp - 4) / 100;
            _lastBarWidth = _process.ReadInt32(context.Ebp - 8);
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.PlanningTimerStarted, _ =>
        {
            var file = $"capture-clock-{_clockCaptures.Count}";
            // The game is stopped at the breakpoint, so nothing draws between the two copies.
            if (!CaptureDrawingArea(window, file, CaptureFixture.Width, CaptureFixture.Height))
                _notes.Add($"{file}: the two copies disagreed.");
            _clockCaptures.Add(new ClockCaptureRecord(_process.ReadInt32(OriginalAddresses.ActivePlayer),
                _process.ReadInt32(OriginalAddresses.ElapsedTurns), file + ".bmp", _lastBarWidth, _lastBarElapsed));
        }, quiet: true);
    }

    // Waits from a Done press (or a turn left to run out) for the next planning phase of the first
    // human. A press the game did not take leaves the turn where it was with no roll made; it is
    // pressed again after a quiet while. In a hot-seat match the later humans' hand-off cards come
    // first: each gets Ready and Done, as does each human's final view once the match is over
    // (FND-OBJECTIVE-004), and an elimination card gets its Done under --pass-cards. The round
    // ends at the first human's hand-off card once elapsed_turns has moved on. Returns why it
    // failed, or null.
    private string? WaitForNextPlanning(IntPtr window, int turn, bool waits, TimeSpan turnTimeout)
    {
        var target = turn;
        var rollsAtClick = _rolls.Count;
        var clicked = DateTime.UtcNow;
        var deadline = DateTime.UtcNow + turnTimeout;
        DateTime? moved = null;
        var pressedSinceTurn = false;
        while (true)
        {
            var next = _process.RunUntil(() =>
            {
                var quiet = DateTime.UtcNow - _process.LastBreakpointUtc;
                if (_awardsReached || _eliminationCardReached || _eliminationCardOpen) return true;
                if (_cardOpen && quiet > TimeSpan.FromSeconds(0.5)) return true;
                // FND-OBJECTIVE-004: a match that ends gives each active human one last look at the
                // city, with the turn's Combat Results open, before the awards controller runs and
                // before elapsed_turns moves on. Close the panels and press Done there.
                if (!_cardOpen && _process.Read(OriginalAddresses.MatchOver, 1)[0] != 0
                    && quiet > TimeSpan.FromSeconds(2)
                    && DateTime.UtcNow - clicked > TimeSpan.FromSeconds(5))
                {
                    _notes.Add($"the match is over; Done pressed at the final view after roll {_rolls.Count}");
                    ClosePanels(window);
                    Click(window, OriginalAddresses.DoneX, OriginalAddresses.DoneY);
                    clicked = DateTime.UtcNow;
                }
                if ((!waits || pressedSinceTurn) && !_cardOpen && _rolls.Count == rollsAtClick
                    && _process.ReadInt32(OriginalAddresses.ElapsedTurns) < target
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

                return !settings.HotSeat && PlanningWaits(moved.Value);
            }, deadline - DateTime.UtcNow);
            if (!next)
                return $"Turn {turn} never reached the next planning phase (match_over "
                    + $"{_process.Read(OriginalAddresses.MatchOver, 1)[0]}, {_panelsOpen} panel(s) open, elapsed_turns "
                    + $"{_process.ReadInt32(OriginalAddresses.ElapsedTurns)}).";
            if (_awardsReached || _eliminationCardReached) return null;
            if (_eliminationCardOpen)
            {
                if (!PassEliminationCard(window)) return $"The elimination card of turn {turn} never closed.";
                rollsAtClick = _rolls.Count;
                clicked = DateTime.UtcNow;
                continue;
            }
            if (!_cardOpen) return null;
            var over = _process.Read(OriginalAddresses.MatchOver, 1)[0] != 0;
            if (moved is not null && !over && _cardPlayer == FirstHuman) return null;
            // A later human of this round, or a human's final view once the match is over.
            var player = _cardPlayer;
            if (HandOff(window, player) is { } failure) return $"Turn {turn}: {failure}";
            if (_eliminationCardOpen) continue;
            if (!ClosePanels(window)) return $"A panel of player {player} in turn {turn} never closed.";
            _notes.Add($"Done of player {player} in turn {turn} pressed after roll {_rolls.Count}");
            Click(window, OriginalAddresses.DoneX, OriginalAddresses.DoneY);
            pressedSinceTurn = true;
            rollsAtClick = _rolls.Count;
            clicked = DateTime.UtcNow;
        }
    }
}
