namespace Rechaos.OriginalProbe;

/// <summary>
/// One step of <c>--order-steps</c>: a double-click on city sector <c>Target</c> (open), a press at
/// <c>(X, Y)</c> inside card <c>Target</c> (card) or at <c>(X, Y)</c> of the window (strip), each
/// answering the popup menu it opens with command <c>Choice</c> (0 closes it with no choice), a
/// double-click at <c>(X, Y)</c> of the window (dbl), a
/// press of the sector view's back control (back), or of a result panel's Exit (exit), or a capture
/// of the drawing area compared at the elements of the screen entries <c>Screens</c> (shot).
/// </summary>
internal sealed record ProbeOrderStep(string Kind, int Target, int X, int Y, int Choice, string? Screens = null)
{
    public override string ToString() => Kind switch
    {
        "shot" => $"capture for {Screens}",
        "open" => $"double-click sector {Target}",
        "card" => $"card {Target} at ({X}, {Y}), command {Choice}",
        "strip" => $"({X}, {Y}), command {Choice}",
        "dbl" => $"double-click ({X}, {Y})",
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
/// beside it, the Overlord bar's marker frame it shows (FND-UI-038) and the pump's counter.
/// </summary>
internal sealed record CaptureShot(
    string File, int MarkerFrame, int PumpCounter, int[] Lamps, int SelectedSector, int? FrameCounter);

internal sealed partial class NewGameSession
{
    private readonly List<OrderStepRecord> _orderSteps = [];

    // RULE-TURN-005, SCR-UI-004: once the dump is taken, the probe opens a sector view and presses
    // the gang cards' strips and the group order strip with posted mouse messages. Each press that
    // opens a popup reaches the TrackPopupMenu call of the popup helper (FND-UI-021); the probe
    // keeps the menu and its items' states there and skips the call, handing the helper the step's
    // command as Windows would for the player's choice, so no menu is shown. An order takes effect
    // only at resolution, which no step reaches, so a step that makes the original roll has gone
    // past the dumped state and ends the run as not dumped. Returns why the steps stopped, or null.
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
        // FND-UI-051: the pump's counter when a panel last stopped the selection frame.
        int? heldCounter = null;
        _process.SetBreakpoint(OriginalAddresses.PanelHoldsSelectionFrame,
            _ => heldCounter = _process.ReadInt32(OriginalAddresses.PumpCounter), quiet: true);
        foreach (var step in settings.OrderSteps!)
        {
            if (step.Kind == "shot")
            {
                // A capture moves nothing, so it is not a post-dump step of the marker log.
                var file = $"capture-step-{_orderSteps.Count}";
                var shot = CaptureDrawingArea(window, file) is var (marker, pump, lamps, selected)
                    ? new CaptureShot(file + ".bmp", marker, pump, lamps, selected,
                        _process.Read(OriginalAddresses.SelectionFrameHeld, 1)[0] == 0 ? pump : heldCounter)
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
                case "exit":
                    PressExitAfterDump(window);
                    break;
            }
            _process.Pump(TimeSpan.FromSeconds(0.8));
            if (_process.Exited) return "The original exited during the order steps.";
            if (_rolls.Count != rollsAtDump)
                return $"The original called roll {_rolls.Count - rollsAtDump} time(s) during the order step {step}.";
            _orderSteps.Add(new OrderStepRecord(step, menu, items,
                _process.ReadInt32(OriginalAddresses.CityViewShown) != 0, SectorCardSlots(), ActiveGangOrders(),
                _process.ReadInt32(OriginalAddresses.SectorViewPlayer)));
        }
        return null;
    }

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
