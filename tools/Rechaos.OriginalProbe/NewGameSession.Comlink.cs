namespace Rechaos.OriginalProbe;

/// <summary>
/// One showing of a Comlink message by the View helper (FND-COMLINK-004): the player and count it
/// was passed, the player's cursor when it was called, and the numbers it drew.
/// </summary>
internal sealed record ComlinkShow(int Player, int Count, int Cursor, List<int> Numbers);

/// <summary>
/// The Comlink state of every player at one point of a run (FMT-STATE-005): comlink_count,
/// comlink_cursor, comlink_pending, and each player's 16 records as hexadecimal bytes.
/// </summary>
internal sealed record ComlinkDump(string Label, int[] Counts, int[] Cursors, int Pending, string[] Records);

/// <summary>
/// One step of a Comlink script and what the original did: the panels open after it, the sounds
/// played during it, the messages View showed, the selection bytes and the draft buffer of the Send
/// panel, each drop of leading read messages (player, count before, count after), and a dump.
/// </summary>
internal sealed class ComlinkStep
{
    public required string Step { get; init; }
    public int ActivePlayer { get; set; }
    public int ElapsedTurns { get; set; }
    public bool SendOpen { get; set; }
    public bool ViewOpen { get; set; }
    public List<string> Entered { get; } = [];
    public List<int> Sounds { get; } = [];
    public List<ComlinkShow> Shows { get; } = [];
    public List<int[]> Drops { get; } = [];
    public string? Selected { get; set; }
    public string? Draft { get; set; }
    public ComlinkDump? Dump { get; set; }
}

/// <summary>
/// Drives the Comlink panels of a local game with several humans from a script, one step per line:
/// <c>visit p</c> waits for player p's planning and presses Ready on the handoff card (RULE-SETUP-008);
/// <c>view</c> and <c>send</c> press the halves of the console's Comlink control; <c>next</c>,
/// <c>prev</c> and <c>dismiss</c> press the View panel's controls; <c>card p</c>, <c>press send</c>
/// and <c>press cancel</c> press the Send panel's; <c>type text</c> posts a key press for each
/// character, with <c>{BACK}</c>, <c>{ENTER}</c>, <c>{LEFT}</c>, <c>{UP}</c>, <c>{RIGHT}</c>,
/// <c>{DOWN}</c> and <c>{EXEC}</c> for those keys; <c>draft</c> keeps the Send panel's draft buffer;
/// <c>dump label</c> keeps every player's Comlink state; <c>done</c> presses Done.
/// </summary>
internal sealed partial class NewGameSession
{
    private readonly List<ComlinkStep> _comlink = [];
    private ComlinkStep? _step;
    private ComlinkShow? _show;
    private bool _sendOpen;
    private bool _viewOpen;
    private int _cardPlayer = -1;
    private bool _cardOpen;

    private void ArmComlink()
    {
        _process.SetBreakpoint(OriginalAddresses.HandoffCard, context =>
        {
            _cardPlayer = context.Argument(0);
            _cardOpen = true;
            _process.SetBreakpoint(context.ReturnAddress, _ => _cardOpen = false, oneShot: true);
        });
        _process.SetBreakpoint(OriginalAddresses.ComlinkSend, context =>
        {
            _sendOpen = true;
            _step?.Entered.Add("send");
            _process.SetBreakpoint(context.ReturnAddress, _ => _sendOpen = false, oneShot: true);
        });
        _process.SetBreakpoint(OriginalAddresses.ComlinkView, context =>
        {
            _viewOpen = true;
            _step?.Entered.Add("view");
            _process.SetBreakpoint(context.ReturnAddress, _ => _viewOpen = false, oneShot: true);
        });
        _process.SetBreakpoint(OriginalAddresses.ComlinkShow, context =>
        {
            var player = context.Argument(0);
            _show = new ComlinkShow(player, context.Argument(1),
                _process.ReadInt32(OriginalAddresses.ComlinkCursor + (uint)(4 * player)), []);
            _step?.Shows.Add(_show);
            _process.SetBreakpoint(context.ReturnAddress, _ => _show = null, oneShot: true);
        });
        _process.SetBreakpoint(OriginalAddresses.NumberDraw, context =>
        {
            if (_show is null) return;
            var call = context.ReturnAddress - 5;
            if (call < OriginalAddresses.ComlinkShow || call > OriginalAddresses.ComlinkShowEnd) return;
            _show.Numbers.Add(context.Argument(2));
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.ComlinkDropRead, context =>
        {
            var player = context.Argument(0);
            var before = _process.ReadInt32(OriginalAddresses.ComlinkCount + (uint)(4 * player));
            var drop = new[] { player, before, -1 };
            _step?.Drops.Add(drop);
            _process.SetBreakpoint(context.ReturnAddress, _ =>
                drop[2] = _process.ReadInt32(OriginalAddresses.ComlinkCount + (uint)(4 * player)), oneShot: true);
        });
        _process.SetBreakpoint(OriginalAddresses.PlaySound, context => _step?.Sounds.Add(context.Argument(0)), quiet: true);
    }

    private string? RunComlink(IntPtr window)
    {
        foreach (var raw in settings.Comlink!)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var space = line.IndexOf(' ');
            var verb = space < 0 ? line : line[..space];
            var argument = space < 0 ? "" : line[(space + 1)..];
            var step = new ComlinkStep { Step = line };
            _step = step;
            _comlink.Add(step);
            var failure = verb switch
            {
                "visit" => Visit(window, int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture)),
                "view" => OpenComlink(window, OriginalAddresses.ComlinkViewX, OriginalAddresses.ComlinkViewY, () => _viewOpen),
                "send" => OpenComlink(window, OriginalAddresses.ComlinkSendX, OriginalAddresses.ComlinkSendY, () => _sendOpen),
                "next" => PanelClick(window, OriginalAddresses.ViewNextX, OriginalAddresses.ViewArrowY),
                "prev" => PanelClick(window, OriginalAddresses.ViewPreviousX, OriginalAddresses.ViewArrowY),
                "dismiss" => CloseComlink(window, OriginalAddresses.PanelButtonX, OriginalAddresses.SendButtonY, () => _viewOpen),
                "card" => Card(window, int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture)),
                "press" when argument == "send" => PanelClick(window, OriginalAddresses.PanelButtonX, OriginalAddresses.SendButtonY),
                "press" when argument == "cancel" => PanelClick(window, OriginalAddresses.PanelButtonX, OriginalAddresses.CancelButtonY),
                "type" => Type(window, argument),
                "draft" => null,
                "dump" => null,
                "done" => Done(window),
                _ => $"Unknown Comlink step {line}.",
            };
            if (failure is not null) return failure;
            _process.Pump(TimeSpan.FromSeconds(0.3));
            step.ActivePlayer = _process.ReadInt32(OriginalAddresses.ActivePlayer);
            step.ElapsedTurns = _process.ReadInt32(OriginalAddresses.ElapsedTurns);
            step.SendOpen = _sendOpen;
            step.ViewOpen = _viewOpen;
            if (_sendOpen || verb is "send" or "press" or "draft")
            {
                step.Selected = Convert.ToHexString(_process.Read(OriginalAddresses.ComlinkSelected, 6));
                step.Draft = Convert.ToHexString(_process.Read(OriginalAddresses.ComlinkDraft, OriginalAddresses.ComlinkRecordSize));
            }
            if (verb is "dump" or "draft" or "visit" or "done" or "press" or "dismiss" or "view" or "send")
                step.Dump = Dump(argument);
            _notes.Add($"comlink step '{line}' after roll {_rolls.Count}: player {step.ActivePlayer}, send open {step.SendOpen}, view open {step.ViewOpen}, sounds [{string.Join(",", step.Sounds)}]");
        }

        _step = null;
        return null;
    }

    private ComlinkDump Dump(string label)
    {
        var counts = Enumerable.Range(0, 6).Select(p => _process.ReadInt32(OriginalAddresses.ComlinkCount + (uint)(4 * p))).ToArray();
        var cursors = Enumerable.Range(0, 6).Select(p => _process.ReadInt32(OriginalAddresses.ComlinkCursor + (uint)(4 * p))).ToArray();
        var records = Enumerable.Range(0, 6).Select(p => Convert.ToHexString(_process.Read(
            OriginalAddresses.ComlinkMessages + (uint)(p * OriginalAddresses.ComlinkPlayerStride),
            OriginalAddresses.ComlinkRecords * OriginalAddresses.ComlinkRecordSize))).ToArray();
        return new ComlinkDump(label, counts, cursors, _process.ReadInt32(OriginalAddresses.ComlinkPending), records);
    }

    // RULE-SETUP-008: with several local humans each planning starts behind the handoff card, then
    // the Combat Results and Last Turn Events panels, then the planning loop.
    private string? Visit(IntPtr window, int player)
    {
        var started = DateTime.UtcNow;
        var pressedAgain = false;
        while (true)
        {
            var waited = _process.RunUntil(() =>
                (_cardOpen && DateTime.UtcNow - _process.LastBreakpointUtc > TimeSpan.FromSeconds(0.5))
                || (_panelsOpen > 0 && DateTime.UtcNow - _process.LastBreakpointUtc > TimeSpan.FromSeconds(0.5))
                || (_planningLoopReached && !_cardOpen && _panelsOpen == 0
                    && DateTime.UtcNow - _process.LastBreakpointUtc > TimeSpan.FromSeconds(0.5)),
                TimeSpan.FromSeconds(20));
            if (!waited)
            {
                if (pressedAgain || DateTime.UtcNow - started > timeout) return $"Player {player}'s planning never came.";
                // A Done press the game did not take: press it again once.
                pressedAgain = true;
                _notes.Add($"Done pressed again while waiting for player {player} after roll {_rolls.Count}");
                Click(window, OriginalAddresses.DoneX, OriginalAddresses.DoneY);
                continue;
            }

            if (_cardOpen)
            {
                if (_cardPlayer != player) return $"The handoff card is for player {_cardPlayer}, not {player}.";
                ArmPlanningLoop();
                for (var attempt = 0; _cardOpen && attempt < 10; attempt++)
                {
                    Click(window, OriginalAddresses.ReadyX, OriginalAddresses.ReadyY);
                    _process.RunUntil(() => !_cardOpen, TimeSpan.FromSeconds(3));
                }
                if (_cardOpen) return $"The handoff card for player {player} never closed.";
                continue;
            }

            if (_panelsOpen > 0)
            {
                if (!ClosePanels(window)) return $"A planning entry panel of player {player} never closed.";
                continue;
            }

            var active = _process.ReadInt32(OriginalAddresses.ActivePlayer);
            return active == player ? null : $"Planning is player {active}'s, not {player}'s.";
        }
    }

    private string? OpenComlink(IntPtr window, int x, int y, Func<bool> open)
    {
        Click(window, x, y);
        // The handler is entered once the console's held-button helper sees the release; a refused
        // panel returns at once, an open one waits for input.
        _process.RunUntil(open, TimeSpan.FromSeconds(3));
        _process.Pump(TimeSpan.FromSeconds(1));
        return null;
    }

    private string? CloseComlink(IntPtr window, int x, int y, Func<bool> open)
    {
        for (var attempt = 0; open() && attempt < 5; attempt++)
        {
            Click(window, x, y);
            _process.RunUntil(() => !open(), TimeSpan.FromSeconds(2));
        }
        return open() ? $"The Comlink panel did not close at ({x}, {y})." : null;
    }

    private string? PanelClick(IntPtr window, int x, int y)
    {
        Click(window, x, y);
        _process.Pump(TimeSpan.FromSeconds(0.7));
        return null;
    }

    private string? Card(IntPtr window, int slot) => PanelClick(window, OriginalAddresses.CardX(slot), OriginalAddresses.CardY(slot));

    // FND-UI-020: the window procedure takes a key from WM_KEYDOWN, the virtual key and the
    // character MapVirtualKeyA gives it; WM_CHAR is not an input event.
    private string? Type(IntPtr window, string text)
    {
        foreach (var key in Keys(text))
        {
            Native.PostMessageW(window, Native.WmKeyDown, key, 1);
            _process.Pump(TimeSpan.FromSeconds(0.12));
            Native.PostMessageW(window, Native.WmKeyUp, key, unchecked((IntPtr)0xC0000001));
            _process.Pump(TimeSpan.FromSeconds(0.05));
        }
        _process.Pump(TimeSpan.FromSeconds(0.3));
        return null;
    }

    internal static IEnumerable<int> Keys(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '{')
            {
                var end = text.IndexOf('}', index);
                var name = text[(index + 1)..end];
                index = end;
                yield return name switch
                {
                    "BACK" => 0x08,
                    "ENTER" => 0x0D,
                    "LEFT" => 0x25,
                    "UP" => 0x26,
                    "RIGHT" => 0x27,
                    "DOWN" => 0x28,
                    "EXEC" => 0x2B,
                    _ => throw new FormatException($"Unknown key {name}."),
                };
                continue;
            }

            var character = text[index];
            yield return character switch
            {
                >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' => character,
                >= 'a' and <= 'z' => char.ToUpperInvariant(character),
                ';' => 0xBA,
                '=' => 0xBB,
                ',' => 0xBC,
                '-' => 0xBD,
                '.' => 0xBE,
                '/' => 0xBF,
                '`' => 0xC0,
                '[' => 0xDB,
                '\\' => 0xDC,
                ']' => 0xDD,
                '\'' => 0xDE,
                _ => throw new FormatException($"No key types {character}."),
            };
        }
    }

    private string? Done(IntPtr window)
    {
        ArmPlanningLoop();
        Click(window, OriginalAddresses.DoneX, OriginalAddresses.DoneY);
        // The planning loop ends and the drop of read messages runs before anything else waits.
        _process.RunUntil(() => _step!.Drops.Count > 0, TimeSpan.FromSeconds(10));
        return _step!.Drops.Count > 0 ? null : "Done was not taken: no Comlink drop followed it.";
    }
}
