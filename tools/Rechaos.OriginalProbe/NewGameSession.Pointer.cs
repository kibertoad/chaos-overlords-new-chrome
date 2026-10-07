namespace Rechaos.OriginalProbe;

/// <summary>
/// One call of the cursor helper (FND-UI-034): the roll count it came after, the Done presses made
/// before it, the shape and force it was passed, and the address of the call.
/// </summary>
internal sealed record PointerCallRecord(int AfterRoll, int Done, int Shape, int Force, uint Call);

internal sealed partial class NewGameSession
{
    private readonly List<PointerCallRecord> _pointerCalls = [];

    // RULE-UI-007: with --pointer the probe records every call of the cursor helper
    // fn_00465BC8(shape, force) (FND-UI-034). The probe posts its clicks and never moves the real
    // pointer, so the window procedure's own calls for WM_SETCURSOR are not made.
    private void ArmPointer() =>
        _process.SetBreakpoint(OriginalAddresses.CursorHelper, context => _pointerCalls.Add(new PointerCallRecord(
            _rolls.Count, _rollsAtDone.Count, context.Argument(0), context.Argument(1), context.ReturnAddress - 5)),
            quiet: true);
}
