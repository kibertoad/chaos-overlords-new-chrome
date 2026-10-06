namespace Rechaos.Game;

/// <summary>
/// The host's choice of whether a match can be watched, and how many turns behind.
/// </summary>
/// <remarks>
/// Off, or a delay of <see cref="Minimum"/> to <see cref="Maximum"/> sealed turns: the bounds the
/// server holds every lobby's settings to (<c>spectatorMinDelayTurns</c> and
/// <c>spectatorMaxDelayTurns</c> in the contracts' limits). The modern lobby steps through the
/// choices in order, off first, so the one control both turns watching on and sets the delay.
/// </remarks>
public static class SpectatorDelayChoice
{
    public const int Minimum = 2;
    public const int Maximum = 20;

    /// <summary>The choice one step up or down from <paramref name="current"/>.</summary>
    /// <remarks>Up from off is the shortest delay, down from the shortest is off; neither end wraps.</remarks>
    public static int? Step(int? current, int direction)
    {
        if (direction > 0) return current is { } up ? Math.Min(Maximum, up + 1) : Minimum;
        if (direction < 0) return current is { } down && down > Minimum ? down - 1 : null;
        return current;
    }

    /// <summary>A delay changed within the bounds, leaving watching on; off stays off.</summary>
    public static int? Adjust(int? current, int direction) =>
        current is { } delay ? Math.Clamp(delay + Math.Sign(direction), Minimum, Maximum) : null;

    /// <summary>What every seat reads about the choice.</summary>
    public static string Label(int? delay) =>
        delay is { } turns ? $"{turns} TURNS BEHIND" : "NOT ALLOWED";

    /// <summary>What a browsed session's row says about watching it: nothing when nobody may.</summary>
    public static string ListingLabel(int? delay) =>
        delay is { } turns ? $"WATCH {turns} BEHIND" : string.Empty;
}
