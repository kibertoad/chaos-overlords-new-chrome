namespace Rechaos.OriginalProbe;

/// <summary>
/// One step of <c>--order-steps</c>: a double-click on city sector <c>Target</c> (open), a press at
/// <c>(X, Y)</c> inside card <c>Target</c> (card) or at <c>(X, Y)</c> of the window (strip), each
/// answering the popup menu it opens with command <c>Choice</c> (0 closes it with no choice), a
/// double-click at <c>(X, Y)</c> of the window (dbl), a
/// press of the sector view's back control (back), or of a result panel's Exit (exit), or a capture
/// of the drawing area compared at the elements of the screen entries <c>Screens</c> (shot), or a
/// wait of <c>Choice</c> milliseconds with no input (wait), or a key press for each character of
/// <c>Text</c> (type).
/// </summary>
internal sealed record ProbeOrderStep(
    string Kind, int Target, int X, int Y, int Choice, string? Screens = null, string? Text = null)
{
    public override string ToString() => Kind switch
    {
        "shot" => $"capture for {Screens}",
        "open" => $"double-click sector {Target}",
        "card" => $"card {Target} at ({X}, {Y}), command {Choice}",
        "strip" => $"({X}, {Y}), command {Choice}",
        "dbl" => $"double-click ({X}, {Y})",
        "wait" => $"wait {Choice} ms",
        "type" => $"type {Text}",
        "keys" => $"keys {Text}",
        "name" => $"name {Text}",
        _ => Kind,
    };
}

/// <summary>
/// A step of <c>--order-steps</c> and what followed it: the popup menu it opened (-1 for none) with
/// each item's command and greyed state (FND-UI-021), whether the city view is shown, the player
/// whose gangs the sector view lists and its card slots, and the order bytes of every gang of the active player in use, slot 80 with
/// them (FMT-STATE-001): slot, sector, action, target, target_2, repeat_action and repeat_target.
/// A shot keeps its capture, null when no two agreeing copies were taken.
/// </summary>
internal sealed record OrderStepRecord(
    ProbeOrderStep Step, int Menu, List<List<int>>? Items, bool CityView, List<int> Cards, List<List<int>> Gangs,
    int Viewed, CaptureShot? Shot = null);

/// <summary>
/// A capture taken after the dump: the bitmap <c>File</c> in the run directory with its repeat
/// beside it, the Overlord bar's marker frame it shows (FND-UI-038) and the pump's counter, the
/// frame of the rotating item pictures of Item Information, Sell or Give (FND-UI-052,
/// FND-UI-053), the idle gang warning's ticks since its open modulo 8 (FND-UI-054), the Comlink
/// Send caret's phase (FND-COMLINK-010), and the tick of the Detailed Combat clip it shows with
/// the clip's index within its presentation (FND-COMBAT-016, FND-COMBAT-011), each null when the
/// screen does not show it or it moved during every copy.
/// </summary>
internal sealed record CaptureShot(
    string File, int MarkerFrame, int PumpCounter, int[] Lamps, int SelectedSector, int? FrameCounter,
    int? ItemFrame = null, int? ClipTick = null, int? IdlePhase = null, int? CaretPhase = null, int? ClipIndex = null);

internal sealed partial class NewGameSession
{
    private readonly List<OrderStepRecord> _orderSteps = [];

    // RULE-TURN-005, SCR-UI-004: once the dump is taken, the probe opens a sector view and presses
    // the gang cards' strips and the group order strip with posted mouse messages. Each press that
    // opens a popup reaches the TrackPopupMenu call of the popup helper (FND-UI-021); the probe
    // keeps the menu and its items' states there and skips the call, handing the helper the step's
    // command as Windows would for the player's choice, so no menu is shown. An order takes effect
    // only at resolution, which no step reaches, so a step that makes the original roll, other than
    // a planning entry's hire offer draws, has gone past the dumped state and ends the run as not
    // dumped. Returns why the steps stopped, or null.
    private string? RecordOrderSteps(IntPtr window)
    {
        var rollsAtDump = _rolls.Count;
        var menu = -1;
        List<List<int>>? items = null;
        int? choice = null;
        _process.SetBreakpoint(OriginalAddresses.PopupMenuTrack, context =>
        {
            menu = _process.ReadInt32(context.Ebp + 8);
            items = MenuItems((IntPtr)_process.ReadInt32(context.Esp));
            context.Eax = (uint)(choice ?? 0);
            context.Esp += 4 * OriginalAddresses.PopupMenuTrackArguments;
            context.Eip = OriginalAddresses.PopupMenuTracked;
        });
        // FND-UI-051: the pump's counter when a panel stopped the selection frame. A panel that
        // slides in over another finds the byte already set and leaves the frame as it was.
        int? heldCounter = null;
        _process.SetBreakpoint(OriginalAddresses.PanelHoldsSelectionFrame, _ =>
        {
            if (_process.Read(OriginalAddresses.SelectionFrameHeld, 1)[0] == 0)
                heldCounter = _process.ReadInt32(OriginalAddresses.PumpCounter);
        }, quiet: true);
        // FND-UI-052, FND-UI-053: the frame local of the last of Item Information, Sell and Give
        // to open, while it runs, and FND-UI-054 the idle gang warning's countdown. It is read
        // from memory around the capture, since a breakpoint's report reaches the probe only after
        // the game has gone on drawing.
        // A return pops the entries down to its own handler's, so a handler whose return went
        // unseen cannot leave its frame to be read by a later shot.
        var itemHandlers = new Stack<(uint Starts, uint Ebp, uint Local, uint ShownLocal)>();
        foreach (var (starts, local, returns, shownLocal) in OriginalAddresses.ItemFrameHandlers)
        {
            _process.SetBreakpoint(starts, context => itemHandlers.Push((starts, context.Ebp, local, shownLocal)), quiet: true);
            _process.SetBreakpoint(returns, _ =>
            {
                if (itemHandlers.Any(entry => entry.Starts == starts))
                    while (itemHandlers.Pop().Starts != starts) { }
            }, quiet: true);
        }
        // FND-UI-054: the warning's line is shown for six ticks from the open and hidden for two,
        // and the countdown holds the ticks left of the current part, so the ticks since the
        // open, modulo 8, are 6 less the countdown while shown and 8 less it while hidden. A read
        // between the handler's stores can find the countdown at 0 while hidden, so the result is
        // reduced modulo 8. The game keeps running between the reads, so the flag is read before
        // and after the countdown, and a pair whose flag changed between them is read again.
        // The open handler's value goes to its own field: the item frame for Item Information,
        // Sell and Give, the idle phase for the warning, the one handler with a shown flag.
        int? ItemFrame() =>
            itemHandlers.TryPeek(out var handler) && handler.ShownLocal == 0
                ? _process.ReadInt32(handler.Ebp - handler.Local)
                : null;
        int? IdlePhase()
        {
            if (!itemHandlers.TryPeek(out var handler) || handler.ShownLocal == 0) return null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var shown = _process.Read(handler.Ebp - handler.ShownLocal, 1)[0];
                var value = _process.ReadInt32(handler.Ebp - handler.Local);
                if (_process.Read(handler.Ebp - handler.ShownLocal, 1)[0] != shown) continue;
                var phase = shown != 0 ? 6 - value : 8 - value;
                return (phase % 8 + 8) % 8;
            }
            return null;
        }
        // FND-COMBAT-016: the frame of the clip that plays, while it runs. The clips of a
        // presentation follow one another, so each start replaces the frame of the one before.
        uint? clipEbp = null;
        _process.SetBreakpoint(OriginalAddresses.CombatClipTickSet, context => clipEbp = context.Ebp, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.CombatClipEnd, _ => clipEbp = null, quiet: true);
        int? ClipTick() => clipEbp is { } ebp
            ? Math.Max(_process.ReadInt32(ebp - OriginalAddresses.CombatClipTick) - 1, 0)
            : null;
        // FND-COMBAT-011: the clip player's entry, where the presentation's clip count moves, comes
        // before the store of its tick local (FND-COMBAT-016), so while a clip's frame is known the
        // count includes it, and its index within the presentation is the count less 1.
        int? ClipIndex() => clipEbp is not null && _detailedCombatOpen && _combatPresentations.Count > 0
            ? _combatPresentations[^1].Clips - 1
            : null;
        foreach (var step in settings.OrderSteps!)
        {
            if (step.Kind == "wait")
            {
                // A wait presses nothing, so it is not a post-dump step of the marker log.
                _process.Pump(TimeSpan.FromMilliseconds(step.Choice));
                if (_process.Exited) return "The original exited during the order steps.";
                if (_rolls.Count != rollsAtDump)
                    return $"The original called roll {_rolls.Count - rollsAtDump} time(s) during the order step {step}.";
                _orderSteps.Add(new OrderStepRecord(step, -1, null,
                    _process.ReadInt32(OriginalAddresses.CityViewShown) != 0, SectorCardSlots(), ActiveGangOrders(),
                    _process.ReadInt32(OriginalAddresses.SectorViewPlayer)));
                continue;
            }
            if (step.Kind == "shot")
            {
                // A capture moves nothing, so it is not a post-dump step of the marker log.
                var file = $"capture-step-{_orderSteps.Count}";
                // The item, the warning line, the Send caret and a clip's tick step every few ticks,
                // so a capture one of them moved under is taken again. Each is read before and
                // after the copy, and kept only when both reads agree. A value that appears during
                // the copy, such as the tick of a clip that started, counts as moved too.
                Func<int?>[] readers = [ItemFrame, IdlePhase, () => CaretFrame(step), ClipTick, ClipIndex];
                string[] names =
                [
                    "the item pictures' frame", "the warning line's phase", "the caret's phase",
                    "the Detailed Combat clip's tick", "the Detailed Combat clip's index",
                ];
                var before = new int?[readers.Length];
                var values = new int?[readers.Length];
                var moved = new bool[readers.Length];
                (int MarkerFrame, int PumpCounter, int[] Lamps, int SelectedSector)? area;
                var attempts = 0;
                bool Moved() => moved.Contains(true);
                do
                {
                    for (var index = 0; index < readers.Length; index++) before[index] = readers[index]();
                    area = CaptureDrawingArea(window, file);
                    for (var index = 0; index < readers.Length; index++)
                    {
                        moved[index] = readers[index]() != before[index];
                        values[index] = moved[index] ? null : before[index];
                    }
                } while (Moved() && ++attempts < 5);
                for (var index = 0; index < readers.Length; index++)
                    if (moved[index])
                        _notes.Add($"{file}: {names[index]} moved during each capture.");
                var shot = area is var (marker, pump, lamps, selected)
                    ? new CaptureShot(file + ".bmp", marker, pump, lamps, selected,
                        _process.Read(OriginalAddresses.SelectionFrameHeld, 1)[0] == 0 ? pump : heldCounter,
                        ItemFrame: values[0], IdlePhase: values[1], CaretPhase: values[2],
                        ClipTick: values[3], ClipIndex: values[4])
                    : null;
                _orderSteps.Add(new OrderStepRecord(step, -1, null,
                    _process.ReadInt32(OriginalAddresses.CityViewShown) != 0, SectorCardSlots(), ActiveGangOrders(),
                    _process.ReadInt32(OriginalAddresses.SectorViewPlayer), shot));
                continue;
            }
            _postDumpStep++;
            menu = -1;
            items = null;
            choice = step.Choice;
            switch (step.Kind)
            {
                case "open":
                    var (x, y) = OriginalAddresses.MapSectorCentre(step.Target);
                    // FND-UI-020: a press clears the double-click phase and the double-click sets it,
                    // which makes it the double-click the planning loop reads.
                    Post(window, Native.WmLButtonDown, 1, x, y);
                    Post(window, Native.WmLButtonUp, 0, x, y);
                    Post(window, Native.WmLButtonDblClk, 1, x, y);
                    Post(window, Native.WmLButtonUp, 0, x, y);
                    break;
                case "dbl":
                    // FND-UI-020: as for open, at any point of the window.
                    Post(window, Native.WmLButtonDown, 1, step.X, step.Y);
                    Post(window, Native.WmLButtonUp, 0, step.X, step.Y);
                    Post(window, Native.WmLButtonDblClk, 1, step.X, step.Y);
                    Post(window, Native.WmLButtonUp, 0, step.X, step.Y);
                    break;
                case "card":
                    Click(window, OriginalAddresses.SectorCardX(step.Target) + step.X,
                        OriginalAddresses.SectorCardY(step.Target) + step.Y);
                    break;
                case "strip":
                    Click(window, step.X, step.Y);
                    break;
                case "back":
                    Click(window, OriginalAddresses.SectorBackX, OriginalAddresses.SectorBackY);
                    break;
                case "warn":
                    // RULE-OPTIONS-003: the run switches Warn if Idle Gangs off before the Done
                    // presses; this step switches it back on for the presses after the dump.
                    _process.Write(OriginalAddresses.PrefWarnIdle, BitConverter.GetBytes(1));
                    break;
                case "exit":
                    PressExitAfterDump(window);
                    break;
                case "type":
                    // FND-UI-020: a key press for each character, as the Comlink script types.
                    Type(window, step.Text!);
                    break;
                case "keys":
                    PressKeys(window, step.Text!);
                    break;
            }
            _process.Pump(TimeSpan.FromSeconds(0.8));
            if (_process.Exited) return "The original exited during the order steps.";
            // RULE-SETUP-008, FND-RNG-006: Ready on the hand-off card begins that human's planning,
            // whose entry draws the hire offers, as the rebuild's planning entry does. Any other
            // roll has gone past the dumped state.
            var past = _rolls.Skip(rollsAtDump).Count(roll => roll.Call != HireOfferDrawCall);
            if (past > 0)
                return $"The original called roll {past} time(s) other than a hire offer draw during the order step {step}.";
            rollsAtDump = _rolls.Count;
            _orderSteps.Add(new OrderStepRecord(step, menu, items,
                _process.ReadInt32(OriginalAddresses.CityViewShown) != 0, SectorCardSlots(), ActiveGangOrders(),
                _process.ReadInt32(OriginalAddresses.SectorViewPlayer)));
        }
        return null;
    }

    // FND-RNG-006: a hire offer draw as the roll log records its call.
    private static readonly string HireOfferDrawCall = $"0x{OriginalAddresses.HireOfferDraw:X8}";

    // FND-COMLINK-010: the Send panel draws the caret's cell inverse while the byte at 0x00498110
    // is 0 and plain while it is set, kept as 3 or 0 timer events since the last flip.
    private int? CaretFrame(ProbeOrderStep step) =>
        step.Screens!.Split(',').Contains("SCR-COMLINK-002")
            ? _process.Read(OriginalAddresses.ComlinkCaretPlain, 1)[0] == 0 ? 3 : 0
            : null;

    // FND-UI-021: menus 1, 2, 3 and 5 each hold one popup of plain items, so every item has a
    // command; MF_GRAYED and MF_DISABLED are the low two bits of its state.
    private static List<List<int>>? MenuItems(IntPtr menu)
    {
        var count = Native.GetMenuItemCount(menu);
        if (count <= 0) return null;
        var items = new List<List<int>>();
        for (var position = 0; position < count; position++)
        {
            var command = Native.GetMenuItemID(menu, position);
            if (command is 0 or uint.MaxValue) continue;
            items.Add([(int)command, (int)(Native.GetMenuState(menu, command, Native.MfByCommand) & 3)]);
        }
        return items;
    }

    private List<int> SectorCardSlots() => Enumerable.Range(0, OriginalAddresses.SectorCards)
        .Select(card => _process.ReadInt32(OriginalAddresses.SectorCardSlots + (uint)(4 * card)))
        .ToList();

    private List<List<int>> ActiveGangOrders()
    {
        var player = _process.ReadInt32(OriginalAddresses.ActivePlayer);
        var records = _process.Read(
            OriginalAddresses.GangRecords + (uint)(player * OriginalAddresses.PlayerGangStride), OriginalAddresses.PlayerGangStride);
        var gangs = new List<List<int>>();
        for (var slot = 0; slot <= 80; slot++)
        {
            var record = records.AsSpan(slot * OriginalAddresses.GangRecordSize, 12);
            var sector = (sbyte)record[2];
            if (sector == 100 && slot < 80) continue;
            gangs.Add([slot, sector, record[7], (sbyte)record[8], (sbyte)record[9], record[10], (sbyte)record[11]]);
        }
        return gangs;
    }
}
