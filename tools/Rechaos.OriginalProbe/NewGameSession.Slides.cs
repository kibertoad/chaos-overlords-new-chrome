namespace Rechaos.OriginalProbe;

/// <summary>
/// One slide-in of a panel (FND-UI-011): the return address of the helper's call, which names the
/// panel's handler (FND-UI-066), the startup benchmark count it read, its travel, and how far right
/// of its final place the panel's left edge was at each copy, 0 for the last.
/// </summary>
internal sealed record SlideRecord(uint Caller, int Benchmark, int Travel, List<int> Offsets);

internal sealed partial class NewGameSession
{
    private readonly List<SlideRecord> _slides = [];

    // RULE-UI-003: with --slides the probe records each slide-in of the panel-open helper
    // fn_0041953E (FND-UI-011, FND-UI-056, EXP-UI-025): the return address on top of the stack at
    // its entry, which FND-UI-066 maps to the handler that called it, the benchmark count at
    // BlitBenchmarkCount, and at each copy of its loop (SlideCopy) the travel at ebp - 4 and the
    // width shown at ebp - 8, and its last copy (SlideFinalCopy). A copy whose helper entry was not
    // seen, because the probe armed in the middle of a slide, is skipped.
    private void ArmSlides()
    {
        _process.SetBreakpoint(OriginalAddresses.PanelOpenHelper, context => _slides.Add(new SlideRecord(
            context.ReturnAddress, _process.ReadInt32(OriginalAddresses.BlitBenchmarkCount), -1, [])),
            quiet: true);
        _process.SetBreakpoint(OriginalAddresses.SlideCopy, context =>
        {
            if (_slides.Count == 0) return;
            var travel = _process.ReadInt32(context.Ebp - 4);
            _slides[^1] = _slides[^1] with { Travel = travel };
            _slides[^1].Offsets.Add(travel - _process.ReadInt32(context.Ebp - 8));
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.SlideFinalCopy, context =>
        {
            if (_slides.Count == 0) return;
            _slides[^1] = _slides[^1] with { Travel = _process.ReadInt32(context.Ebp - 4) };
            _slides[^1].Offsets.Add(0);
        }, quiet: true);
    }
}
