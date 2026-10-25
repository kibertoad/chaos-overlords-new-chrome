namespace Rechaos.Core.GameModel;

/// <summary>
/// Where the Player Rankings panel places each active player's portrait on its rail
/// (SCR-OBJECTIVE-001).
/// </summary>
public static class ScenarioScoreRail
{
    /// <summary>The length of a rail, in pixels, over which the scores are spread (SCR-OBJECTIVE-001).</summary>
    public const int Length = 140;

    /// <summary>
    /// SCR-OBJECTIVE-001, FND-OBJECTIVE-005: the score's distance from the highest, scaled by a
    /// single-precision 140 / (high - low + 1) and cut toward zero; 70 when every score is equal.
    /// </summary>
    public static int Offset(long score, long high, long low)
    {
        var range = high - low + 1;
        if (range == 1) return Length / 2;
        var factor = (float)((double)Length / range);
        return (int)((double)(high - score) * factor);
    }
}
