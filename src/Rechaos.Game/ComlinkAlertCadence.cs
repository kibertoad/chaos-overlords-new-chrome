namespace Rechaos.Game;

/// <summary>
/// Presentation-only cadence for the original repeating unread-Comlink alert.
/// </summary>
public sealed class ComlinkAlertCadence
{
    /// <summary>
    /// Twenty-four ticks of the presentation clock (RULE-UI-008, SCR-UI-003), 3984 ms.
    /// </summary>
    public const int RepeatTicks = 24;

    public static readonly TimeSpan RepeatInterval = PresentationClock.Period * RepeatTicks;

    private TimeSpan? _nextAlert;

    public bool Advance(bool hasUnread, bool presentationActive, TimeSpan now)
    {
        if (!hasUnread)
        {
            _nextAlert = null;
            return false;
        }
        if (!presentationActive) return false;
        if (_nextAlert is { } next && now < next) return false;

        _nextAlert = now + RepeatInterval;
        return true;
    }
}
