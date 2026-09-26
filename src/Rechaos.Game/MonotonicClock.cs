using System.Diagnostics;

namespace Rechaos.Game;

/// <summary>
/// The clock the client measures how long something has lasted with.
/// </summary>
/// <remarks>
/// Not the wall clock: a clock that the system or a time sync sets back holds a grace for as long as
/// it was set back, and one set forward skips it. A reading is only meaningful against another
/// reading from this clock; a moment that is shown, stored or compared with the server's time is a
/// <see cref="DateTimeOffset"/> instead.
/// </remarks>
internal static class MonotonicClock
{
    /// <summary>Time since an arbitrary fixed origin; it never goes backwards.</summary>
    public static TimeSpan Now => Stopwatch.GetElapsedTime(0);
}
