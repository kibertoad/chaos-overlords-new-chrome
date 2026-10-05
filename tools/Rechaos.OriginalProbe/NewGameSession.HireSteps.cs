namespace Rechaos.OriginalProbe;

/// <summary>
/// One step of <c>--hire-steps</c>: an offer slot dragged onto a sector, its Reject cross pressed
/// (sector -2), or the Exit control of a result panel still open at the endpoint pressed (slot -1).
/// </summary>
internal sealed record ProbeHireStep(int Slot, int Sector)
{
    public override string ToString() => Slot == -1 ? "exit" : Sector == -2 ? $"reject {Slot}" : $"drag {Slot} to sector {Sector}";
}

/// <summary>A step of <c>--hire-steps</c> and every player's hire_orders after it (FND-HIRE-001).</summary>
internal sealed record HireStepRecord(int Slot, int Sector, List<int> Orders);

internal sealed partial class NewGameSession
{
    private readonly List<HireStepRecord> _hireSteps = [];

    // RULE-HIRE-003, FND-HIRE-008: once the dump is taken, the probe drags offers and presses their
    // Reject crosses with posted mouse messages, as a player does on the console's Hire dock, and
    // keeps hire_orders after each step. An order only takes effect at resolution, which no step
    // reaches, so the steps leave the dumped state as it was.
    private bool RecordHireSteps(IntPtr window)
    {
        foreach (var step in settings.HireSteps!)
        {
            _postDumpStep++;
            if (step.Slot == -1)
                Click(window, OriginalAddresses.PanelExitX, OriginalAddresses.PanelExitY);
            else if (step.Sector == -2)
                Click(window, OriginalAddresses.HireRejectX(step.Slot), OriginalAddresses.HireRejectY);
            else
            {
                var (fromX, fromY) = (OriginalAddresses.HireOfferX(step.Slot), OriginalAddresses.HireOfferY);
                var (toX, toY) = OriginalAddresses.MapSectorCentre(step.Sector);
                // The Hire handler follows the two pointer points the window procedure keeps from
                // WM_MOUSEMOVE, one of them from GetCursorPos (FND-UI-020), so the probe writes both
                // itself instead of moving the desktop's cursor, and posts only the button messages.
                PointerAt(fromX, fromY);
                Post(window, Native.WmLButtonDown, 1, fromX, fromY);
                _process.Pump(TimeSpan.FromSeconds(0.3));
                PointerAt((fromX + toX) / 2, (fromY + toY) / 2);
                _process.Pump(TimeSpan.FromSeconds(0.2));
                PointerAt(toX, toY);
                _process.Pump(TimeSpan.FromSeconds(0.3));
                Post(window, Native.WmLButtonUp, 0, toX, toY);
            }
            _process.Pump(TimeSpan.FromSeconds(0.8));
            if (_process.Exited) return false;
            _hireSteps.Add(new HireStepRecord(step.Slot, step.Sector,
                _process.Read(OriginalAddresses.HireOrders, 18).Select(value => (int)(sbyte)value).ToList()));
        }
        return true;
    }

    private void PointerAt(int x, int y)
    {
        var point = BitConverter.GetBytes((y << 16) | (x & 0xFFFF));
        _process.Write(OriginalAddresses.PointerClientPoint, point);
        _process.Write(OriginalAddresses.PointerScreenPoint, point);
    }

    private static void Post(IntPtr window, uint message, int buttons, int x, int y) =>
        Native.PostMessageW(window, message, buttons, (IntPtr)((y << 16) | (x & 0xFFFF)));
}
