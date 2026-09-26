namespace Rechaos.Game;

/// <summary>
/// The in-game calendar the city, Comlink and Last Turn panels print: one week per turn, 52 weeks
/// a year, from week 1 of 2050.
/// </summary>
public static class MatchCalendar
{
    public const int FirstYear = 2050;
    public const int WeeksPerYear = 52;

    /// <summary>The year and the week from 1 of the week counted from 0.</summary>
    public static (int Year, int Week) Of(int weekIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(weekIndex);
        return (FirstYear + weekIndex / WeeksPerYear, weekIndex % WeeksPerYear + 1);
    }
}
