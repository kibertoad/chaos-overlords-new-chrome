namespace Rechaos.OriginalProbe;

/// <summary>
/// One slide-in of a panel (FND-UI-011): the startup benchmark count it read, its travel, and how
/// far right of its final place the panel's left edge was at each copy, 0 for the last.
/// </summary>
internal sealed record SlideRecord(int Benchmark, int Travel, List<int> Offsets);

internal sealed partial class NewGameSession
{
    private readonly List<SlideRecord> _slides = [];

    // RULE-UI-003: with --slides the probe records each slide-in of the panel-open helper
    // fn_0041953E (FND-UI-011, EXP-UI-025): the benchmark count at BlitBenchmarkCount, and at each
    // copy of its loop (SlideCopy) the travel at ebp - 4 and the width shown at ebp - 8, and its last
    // copy (SlideFinalCopy).
    private void ArmSlides()
    {
        _process.SetBreakpoint(OriginalAddresses.PanelOpenHelper, _ => _slides.Add(
            new SlideRecord(_process.ReadInt32(OriginalAddresses.BlitBenchmarkCount), -1, [])), quiet: true);
        _process.SetBreakpoint(OriginalAddresses.SlideCopy, context =>
        {
            var travel = _process.ReadInt32(context.Ebp - 4);
            _slides[^1] = _slides[^1] with { Travel = travel };
            _slides[^1].Offsets.Add(travel - _process.ReadInt32(context.Ebp - 8));
        }, quiet: true);
        _process.SetBreakpoint(OriginalAddresses.SlideFinalCopy, context =>
        {
            _slides[^1] = _slides[^1] with { Travel = _process.ReadInt32(context.Ebp - 4) };
            _slides[^1].Offsets.Add(0);
        }, quiet: true);
    }
}
