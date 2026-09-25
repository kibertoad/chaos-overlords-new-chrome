namespace Rechaos.Game;

/// <summary>
/// The slide-in of a panel (RULE-UI-003). The panel comes in from x 448 in whole steps worked out
/// from a copy benchmark, one copy per step, and the last copy puts it in place. The slide-out is
/// not animated (DEV-UI-001).
/// </summary>
public sealed class PanelSlideTransition
{
    public const int StartOffset = 344;
    public const int AlternateStartOffset = 320;
    public const int MinimumStep = 16;

    /// <summary>
    /// The copies per second the rebuild uses in place of the original's startup benchmark
    /// (RULE-UI-003, RULE-TIMER-004). The original counts how many screen copies the machine makes
    /// in about a second, so its slide pace follows the machine. At 84 the divisor is 21, both
    /// travels take the 16-pixel minimum step, which any machine counting 84 or more copies gets,
    /// and the 22 copies of the 344-pixel slide last 22/84 of a second.
    /// </summary>
    public const int NominalBlitBenchmarkCount = 84;

    private ClientScreen? _screen;
    private TimeSpan _started;
    private int _startOffset;

    public void Begin(ClientScreen screen, TimeSpan now)
    {
        if (now < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(now));
        _screen = IsPanel(screen) ? screen : null;
        _startOffset = StartOffsetFor(screen);
        _started = now;
    }

    public void Begin(ClientScreen previous, ClientScreen current, TimeSpan now)
    {
        if (now < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(now));
        _screen = ShouldAnimate(previous, current) ? current : null;
        _startOffset = StartOffsetFor(current);
        _started = now;
    }

    /// <summary>
    /// How far right of its final place the panel's left edge is at <paramref name="now"/>. Copy
    /// <c>k</c> of RULE-UI-003's sequence is shown from <c>k / NominalBlitBenchmarkCount</c>
    /// seconds after the slide starts.
    /// </summary>
    public int Offset(ClientScreen screen, TimeSpan now)
    {
        if (_screen != screen || now < _started) return 0;
        var offsets = SlideInOffsets(
            _startOffset, SlideStep(_startOffset, NominalBlitBenchmarkCount), slidePanels: true);
        var copy = (now - _started).Ticks * NominalBlitBenchmarkCount / TimeSpan.TicksPerSecond;
        if (copy >= offsets.Count - 1)
        {
            _screen = null;
            return 0;
        }
        return offsets[(int)copy];
    }

    public int Offset(ClientScreen screen, TimeSpan now, bool enabled)
    {
        if (enabled) return Offset(screen, now);
        Clear();
        return 0;
    }

    public void Clear() => _screen = null;

    /// <summary>How long the slide-in of a panel with <paramref name="travel"/> lasts.</summary>
    public static TimeSpan DurationFor(int travel) => TimeSpan.FromTicks(
        SlideInOffsets(travel, SlideStep(travel, NominalBlitBenchmarkCount), slidePanels: true).Count
        * TimeSpan.TicksPerSecond / NominalBlitBenchmarkCount);

    /// <summary>RULE-UI-003 <c>slide_step</c>: a quarter of the benchmark count, at least 1, divides the travel.</summary>
    public static int SlideStep(int travel, int blitBenchmarkCount)
    {
        var divisor = blitBenchmarkCount / 4;
        if (divisor < 1) divisor = 1;
        var step = travel / divisor;
        if (step < MinimumStep) step = MinimumStep;
        return step;
    }

    /// <summary>
    /// RULE-UI-003's opening sequence: the offset of each copy, ending with 0. The loop stops at
    /// the last whole step below the travel, and with Slide Panels off only the final copy is made.
    /// </summary>
    public static IReadOnlyList<int> SlideInOffsets(int travel, int step, bool slidePanels)
    {
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step));
        var offsets = new List<int>();
        if (slidePanels)
            for (var shown = step; shown < travel; shown += step)
                offsets.Add(travel - shown);
        offsets.Add(0);
        return offsets;
    }

    public static bool ShouldAnimate(ClientScreen previous, ClientScreen current) =>
        IsPanel(current) && current != ClientScreen.Commands;

    /// <summary>
    /// The travel of a panel: 320 for the alternate crop of Item Information, Site Information,
    /// the Financial panels, the Hire comparison and Game Information, 344 for the others,
    /// Gangs in Sector among them (RULE-UI-003, FND-UI-011).
    /// </summary>
    public static int StartOffsetFor(ClientScreen screen) => screen is
        ClientScreen.GameInfo or ClientScreen.Hire
            or ClientScreen.Site or ClientScreen.ItemInformation or ClientScreen.Finance
        ? AlternateStartOffset
        : StartOffset;

    /// <summary>
    /// The screens that open as panels. The detailed sector screen is a full view that the
    /// original draws in place, so it neither slides nor plays the panel sounds (RULE-UI-003).
    /// </summary>
    public static bool IsPanel(ClientScreen screen) => screen is
        ClientScreen.Options or ClientScreen.Help or ClientScreen.GameInfo or ClientScreen.Commands
        or ClientScreen.Hire or ClientScreen.Events or ClientScreen.ComlinkView
        or ClientScreen.ComlinkSend or ClientScreen.SectorGangs
        or ClientScreen.Gang or ClientScreen.Site or ClientScreen.ItemInformation
        or ClientScreen.Finance or ClientScreen.Ranking or ClientScreen.Items
        or ClientScreen.Give or ClientScreen.GiveTarget or ClientScreen.Sell
        or ClientScreen.CombatSummary or ClientScreen.Search;
}
