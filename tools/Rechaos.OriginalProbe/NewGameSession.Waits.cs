namespace Rechaos.OriginalProbe;

/// <summary>
/// One call of the presentation wait (FND-TIMER-002): its argument, the address of the call, and
/// the milliseconds of its start and its return, counted from when the probe armed the recording,
/// as the ticks are.
/// </summary>
internal sealed record WaitRecord(int Ticks, uint Call, long Started)
{
    public long Returned { get; set; } = -1;
}

internal sealed partial class NewGameSession
{
    private readonly List<WaitRecord> _waits = [];
    private readonly List<long> _ticks = [];
    private readonly System.Diagnostics.Stopwatch _waitClock = new();

    // RULE-TIMER-004: with --waits the probe records, from the dump on, every tick of timer slot 0,
    // the six-per-second presentation clock, through the timer callback fn_004327C0 (its slot is
    // the third argument), and every call of the wait fn_00464CD9(n) with the time it returns
    // (FND-TIMER-002).
    private void ArmWaits()
    {
        _waitClock.Start();
        _process.SetBreakpoint(OriginalAddresses.TimerCallback, context =>
        {
            if (context.Argument(2) == 0) _ticks.Add(_waitClock.ElapsedMilliseconds);
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.PresentationWait, context =>
        {
            var wait = new WaitRecord(context.Argument(0), context.ReturnAddress - 5, _waitClock.ElapsedMilliseconds);
            _waits.Add(wait);
            _process.SetBreakpoint(context.ReturnAddress, _ => wait.Returned = _waitClock.ElapsedMilliseconds,
                oneShot: true, quiet: true);
        }, quiet: true);
    }
}
