using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The slide-in of a panel (RULE-UI-003). The panel comes in from x 448 in whole steps worked out
/// from a copy benchmark, one copy per step, and the last copy puts it in place. Each copy draws
/// only the panel's left columns, cut off at the panel's right edge, over the screen the panel
/// opened from. The slide-out is not animated (DEV-UI-001).
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

    /// <summary>The right edge of the panel area every original panel ends at (RULE-UI-003).</summary>
    public const int PanelRight = 448;
    public const int PanelTop = 124;
    public const int PanelHeight = 209;

    private ClientScreen? _screen;
    private TimeSpan _started;
    private int _startOffset;

    /// <summary>The screen the sliding panel opened from, drawn around and under it.</summary>
    public ClientScreen? Previous { get; private set; }

    /// <summary>Where the sliding panel ends up, in the 640-by-460 virtual screen.</summary>
    public Rectangle PanelArea { get; private set; }

    public void Begin(ClientScreen screen, TimeSpan now)
    {
        if (now < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(now));
        _screen = IsPanel(screen) ? screen : null;
        _startOffset = StartOffsetFor(screen);
        _started = now;
        Previous = null;
        PanelArea = PanelAreaFor(screen);
    }

    /// <param name="compactGangPanel">
    /// Whether a Gang screen being opened is the compact panel of SCR-GANG-001, which slides in the
    /// alternate form.
    /// </param>
    public void Begin(ClientScreen previous, ClientScreen current, TimeSpan now,
        bool compactGangPanel = false)
    {
        if (now < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(now));
        _screen = ShouldAnimate(previous, current) ? current : null;
        _startOffset = StartOffsetFor(current, compactGangPanel);
        _started = now;
        Previous = previous;
        PanelArea = PanelAreaFor(current, compactGangPanel);
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

    /// <summary>
    /// RULE-UI-003: the final place of a panel, <c>(104,124,344,209)</c> for a primary panel and
    /// <c>(128,124,320,209)</c> for an alternate one. The rebuild's Options and Help panels are
    /// larger than the original's and keep their own rectangles, so they are cut off at their own
    /// right edges.
    /// </summary>
    public static Rectangle PanelAreaFor(ClientScreen screen, bool compactGangPanel = false)
    {
        if (screen == ClientScreen.Options) return OptionsLayout.Panel;
        if (screen == ClientScreen.Help) return HelpLayout.Panel;
        var travel = StartOffsetFor(screen, compactGangPanel);
        return new Rectangle(PanelRight - travel, PanelTop, travel, PanelHeight);
    }

    /// <summary>
    /// RULE-UI-003: the part of the screen a copy at <paramref name="offset"/> draws. The panel's
    /// left edge stands <paramref name="offset"/> pixels right of its final place and it is cut
    /// off at its final right edge, so only its left <c>width - offset</c> columns show.
    /// </summary>
    public static Rectangle VisibleArea(Rectangle panel, int offset)
    {
        var shift = Math.Clamp(offset, 0, panel.Width);
        return new Rectangle(panel.X + shift, panel.Y, panel.Width - shift, panel.Height);
    }

    public static bool ShouldAnimate(ClientScreen previous, ClientScreen current) =>
        IsPanel(current) && current != ClientScreen.Commands;

    /// <summary>
    /// RULE-UI-003: the six panels drawn from the 320-pixel alternate crop travel 320 pixels, the
    /// others 344. Gangs in Sector is a primary panel (SCR-UI-005, FND-UI-014); the Gang screen is
    /// the alternate SCR-GANG-001 only when an order panel opened it.
    /// </summary>
    public static int StartOffsetFor(ClientScreen screen, bool compactGangPanel = false) =>
        screen is ClientScreen.GameInfo or ClientScreen.Hire
            or ClientScreen.Site or ClientScreen.ItemInformation or ClientScreen.Finance
        || (screen == ClientScreen.Gang && compactGangPanel)
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
