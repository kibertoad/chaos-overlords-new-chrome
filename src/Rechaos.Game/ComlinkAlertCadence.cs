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

    private long? _nextAlertTick;
    private long? _deliverySequence;

    public bool Advance(bool hasUnread, bool presentationActive, TimeSpan now,
        bool enteringPlanning = false, long? deliverySequence = null)
    {
        var tick = PresentationClock.Ticks(now);
        if (!hasUnread)
        {
            _nextAlertTick = null;
            _deliverySequence = null;
            return false;
        }
        if (!presentationActive) return false;
        // RULE-AUDIO-008, FND-AUDIO-012: arrival and planning entry reset only the
        // modulo-three repeat counter; the shared eight-step blink phase survives.
        var restart = enteringPlanning
            || deliverySequence is { } sequence && sequence != _deliverySequence;
        _deliverySequence = deliverySequence;
        if (!restart && _nextAlertTick is { } next && tick < next) return false;
        if (restart || _nextAlertTick is null)
            _nextAlertTick = (tick / 8 + 3) * 8;
        else
            _nextAlertTick += ((tick - _nextAlertTick.Value) / RepeatTicks + 1) * RepeatTicks;
        return true;
    }
}
