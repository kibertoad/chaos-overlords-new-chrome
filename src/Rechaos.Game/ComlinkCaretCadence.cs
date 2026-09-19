namespace Rechaos.Game;

/// <summary>
/// Presentation-only reproduction of Comlink Send's timer-zero cursor blink.
/// </summary>
/// <remarks>
/// Native initialization registers timer zero through <c>timeSetEvent</c> at
/// <c>1000 / 6</c> milliseconds. Send keeps its normal glyph row for three
/// consumed timer events, then alternates to the inverse row for the next
/// three. This class deliberately has no connection to match state.
/// </remarks>
public sealed class ComlinkCaretCadence
{
    public const int EventsPerGlyphRow = 3;
    public static readonly TimeSpan TimerEventInterval = TimeSpan.FromMilliseconds(1000 / 6);

    private TimeSpan _nextTimerEvent;
    private int _eventsInGlyphRow;

    public bool UsesInverseGlyph { get; private set; }

    public void Reset(TimeSpan now)
    {
        _nextTimerEvent = now + TimerEventInterval;
        _eventsInGlyphRow = 0;
        UsesInverseGlyph = false;
    }

    public void Advance(TimeSpan now)
    {
        while (now >= _nextTimerEvent)
        {
            _nextTimerEvent += TimerEventInterval;
            if (++_eventsInGlyphRow != EventsPerGlyphRow) continue;

            _eventsInGlyphRow = 0;
            UsesInverseGlyph = !UsesInverseGlyph;
        }
    }
}
