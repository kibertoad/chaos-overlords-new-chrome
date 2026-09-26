namespace Rechaos.Game;

/// <summary>
/// The original's six-per-second presentation clock (RULE-UI-008).
/// </summary>
/// <remarks>
/// The original starts a multimedia timer at <c>1000 / 6</c> ms, 166 in integer arithmetic, and
/// the loops that animate or pause read its flag. The rebuild has no second thread to raise a flag,
/// so the ticks are counted from the game clock: tick <c>n</c> falls at <c>n * 166</c> ms after the
/// program started, and every reader shares that one phase. A reader gets the count the flag would
/// give a loop that never fell behind. The original's flag drops the ticks that fall while a loop
/// is busy for longer than a period; the rebuild counts them.
/// </remarks>
public static class PresentationClock
{
    /// <summary>The period of the clock in milliseconds, <c>1000 / 6</c> as integer division.</summary>
    public const int PeriodMilliseconds = 1000 / 6;

    /// <summary>The period of the clock.</summary>
    public static readonly TimeSpan Period = TimeSpan.FromMilliseconds(PeriodMilliseconds);

    /// <summary>How many ticks have fallen at or before <paramref name="now"/>.</summary>
    public static long Ticks(TimeSpan now)
    {
        if (now < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(now));
        return now.Ticks / Period.Ticks;
    }

    /// <summary>
    /// Whether a blinking console light is lit: two ticks lit and two dark, the rhythm the Events
    /// and Comlink lights take from the event pump's blink step (SCR-UI-003).
    /// </summary>
    public static bool BlinkLit(TimeSpan now) => Ticks(now) / 2 % 2 == 0;
}
