namespace Rechaos.Game;

/// <summary>
/// How often a window without focus is redrawn.
/// </summary>
/// <remarks>
/// A window nobody is looking at still has to be serviced: online notices arrive, the planning
/// timer runs down, the autosave is pumped. Rebuilding the whole interface dozens of times a
/// second for it is what costs CPU, GPU and battery, so the redraw is what gets spaced out while
/// the update keeps its foreground cadence. The interval is short enough that a background online
/// turn still shows up promptly.
/// </remarks>
public sealed class BackgroundRedrawCadence
{
    public static readonly TimeSpan RedrawInterval = TimeSpan.FromMilliseconds(200);

    private TimeSpan? _nextRedraw;

    /// <summary>Whether this frame is drawn and presented; a yes schedules the next one.</summary>
    public bool ShouldRedraw(bool windowActive, TimeSpan now)
    {
        if (windowActive)
        {
            _nextRedraw = null;
            return true;
        }
        if (_nextRedraw is { } next && now < next) return false;

        _nextRedraw = now + RedrawInterval;
        return true;
    }
}
