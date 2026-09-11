namespace Rechaos.Game;

/// <summary>
/// Presentation-only cadence for the original repeating unread-Comlink alert.
/// </summary>
public sealed class ComlinkAlertCadence
{
    public static readonly TimeSpan RepeatInterval = TimeSpan.FromSeconds(4);

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
