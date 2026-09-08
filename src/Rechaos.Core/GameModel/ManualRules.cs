namespace Rechaos.Core.GameModel;

/// <summary>
/// Small, unambiguous rules stated by the original manual. These are separated
/// from binary-verified rules so intended behavior and compatibility evidence
/// are not conflated.
/// </summary>
public static class ManualRules
{
    public const int MaximumForce = 10;
    public const int BribeCost = 3;
    public const int BribeToleranceIncrease = 5;
    public const int SnitchToleranceDecrease = 3;
    public const int MinimumTolerance = 0;
    public const int MaximumTolerance = 40;
    public const int PoliceCombat = 20;
    public const int PoliceDetect = 12;

    public static int ApplyBribe(int tolerance)
    {
        ValidateTolerance(tolerance);
        return Math.Min(MaximumTolerance, tolerance + BribeToleranceIncrease);
    }

    public static int ApplySnitch(int tolerance)
    {
        ValidateTolerance(tolerance);
        return Math.Max(MinimumTolerance, tolerance - SnitchToleranceDecrease);
    }

    public static bool IsDieSuccess(int value)
    {
        if (value is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(value), value, "A die must be 1-6.");
        return value >= 4;
    }

    public static int CountSuccesses(IEnumerable<int> rolls)
    {
        ArgumentNullException.ThrowIfNull(rolls);
        return rolls.Count(IsDieSuccess);
    }

    /// <summary>Manual-stated chance that police detect a gang during crackdown.</summary>
    public static int PoliceDetectionPercent(int stealth)
    {
        if (stealth < 0) throw new ArgumentOutOfRangeException(nameof(stealth));
        return Math.Clamp(100 - Math.Max(0, stealth - 5) * 5, 0, 100);
    }

    public static int RestoreForce(int currentForce, int successes)
    {
        if (currentForce is < 0 or > MaximumForce) throw new ArgumentOutOfRangeException(nameof(currentForce));
        if (successes < 0) throw new ArgumentOutOfRangeException(nameof(successes));
        return Math.Min(MaximumForce, currentForce + successes);
    }

    private static void ValidateTolerance(int tolerance)
    {
        if (tolerance is < MinimumTolerance or > MaximumTolerance)
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be 0-40.");
    }
}
