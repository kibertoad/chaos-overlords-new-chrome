namespace Rechaos.Core.GameModel;

/// <summary>
/// A sector's Tolerance in two parts: the base that Bribe, Snitch and the return toward normal
/// change during resolution, and the completed sites' Tolerance that the rebuild before planning
/// adds on top of it (RULE-TOLERANCE-001, RULE-TOLERANCE-002, RULE-SITE-001).
/// </summary>
public static class ToleranceResolver
{
    public const int IncomeToleranceSum = 17;

    /// <summary>RULE-TOLERANCE-002: the range the base is clamped to after the instant phase.</summary>
    public const int MinimumBaseTolerance = 1;
    public const int MaximumBaseTolerance = 40;

    /// <summary>The value the base Tolerance returns toward: 17 minus the sector's Income.</summary>
    public static int NormalBaseTolerance(MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(sector);
        return IncomeToleranceSum - sector.Income;
    }

    /// <summary>
    /// The Tolerance of the sector's completed sites, which the rebuild before planning adds to
    /// the base (RULE-SITE-001).
    /// </summary>
    public static int SiteAdjustment(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        return sector.Sites
            .Where(site => SiteControlRules.Controller(sector, site) is not null)
            .Sum(site => state.Definitions.Site(site.DefinitionId).Tolerance);
    }

    /// <summary>RULE-BRIBE-001: the base plus 3, stored as a signed byte.</summary>
    public static int ApplyBribe(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        return SignedByte(sector.BaseTolerance + ManualRules.BribeToleranceIncrease);
    }

    /// <summary>RULE-SNITCH-001: the base minus 3, stored as a signed byte.</summary>
    public static int ApplySnitch(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        return SignedByte(sector.BaseTolerance - ManualRules.SnitchToleranceDecrease);
    }

    /// <summary>RULE-TOLERANCE-002: every base Tolerance clamped to 1..40 after the instant phase.</summary>
    internal static void ClampAfterInstant(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var sector in state.Sectors)
            sector.BaseTolerance = Math.Clamp(
                sector.BaseTolerance, MinimumBaseTolerance, MaximumBaseTolerance);
    }

    public static int MoveOnePointToward(int current, int target)
    {
        return current == target ? current : current + Math.Sign(target - current);
    }

    /// <summary>
    /// RULE-TOLERANCE-001: at the start of resolution every base Tolerance moves one point toward
    /// 17 minus the sector's Income. The sites take no part.
    /// </summary>
    internal static void StepTowardNormal(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var sector in state.Sectors)
            sector.BaseTolerance = MoveOnePointToward(sector.BaseTolerance, NormalBaseTolerance(sector));
    }

    /// <summary>
    /// RULE-SITE-001: before planning, the Tolerance the Chaos test reads is rebuilt as the base
    /// plus the completed sites' Tolerance, each sum stored as a signed byte.
    /// </summary>
    internal static void RebuildBeforePlanning(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var sector in state.Sectors)
        {
            var tolerance = sector.BaseTolerance;
            foreach (var site in sector.Sites)
                if (SiteControlRules.Controller(sector, site) is not null)
                    tolerance = SignedByte(tolerance + state.Definitions.Site(site.DefinitionId).Tolerance);
            sector.Tolerance = tolerance;
        }
    }

    private static int SignedByte(int value) => unchecked((sbyte)value);
}
