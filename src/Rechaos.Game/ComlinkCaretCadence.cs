namespace Rechaos.Game;

/// <summary>
/// Presentation-only reproduction of Comlink Send's timer-zero cursor blink.
/// </summary>
/// <remarks>
/// Native initialization registers timer zero through <c>timeSetEvent</c> at
/// <c>1000 / 6</c> milliseconds. Send keeps its normal glyph row for three
/// consumed timer events, then alternates to the inverse row for the next
/// three. The game passes the time of its event pump clock, which stops while
/// Cancel or Send is held, so the caret stops with it and the release takes one
/// tick (FND-UI-047). This class deliberately has no connection to match state.
/// </remarks>
public sealed class ComlinkCaretCadence
{
    public const int EventsPerGlyphRow = 3;
    public static readonly TimeSpan TimerEventInterval = PresentationClock.Period;

    private long _lastTick;
    private int _eventsInGlyphRow;

    public bool UsesInverseGlyph { get; private set; }

    public void Reset(TimeSpan now)
    {
        _lastTick = PresentationClock.Ticks(now);
        _eventsInGlyphRow = 0;
        UsesInverseGlyph = false;
    }

    /// <summary>Consumes the presentation ticks that fell since the last call.</summary>
    public void Advance(TimeSpan now)
    {
        var tick = PresentationClock.Ticks(now);
        for (; _lastTick < tick; _lastTick++)
        {
            if (++_eventsInGlyphRow != EventsPerGlyphRow) continue;

            _eventsInGlyphRow = 0;
            UsesInverseGlyph = !UsesInverseGlyph;
        }
    }
}
