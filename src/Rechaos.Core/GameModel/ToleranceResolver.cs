namespace Rechaos.Core.GameModel;

/// <summary>
/// Restores temporary Bribe and Snitch changes one point per turn toward the
/// sector's income-derived tolerance, including currently influenced sites.
/// </summary>
public static class ToleranceResolver
{
    public const int IncomeToleranceSum = 17;

    public static int NormalTolerance(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        if (state.Sectors.Count <= sector.Id || state.Sectors[sector.Id] != sector)
            throw new ArgumentException("Sector does not belong to the match.", nameof(sector));

        var siteAdjustment = sector.Sites
            .Where(site => site.InfluencedBy is not null)
            .Sum(site => state.Definitions.Sites.Single(
                definition => definition.Id == site.DefinitionId).Tolerance);
        return Math.Clamp(
            checked(IncomeToleranceSum - sector.Income + siteAdjustment),
            ManualRules.MinimumTolerance,
            ManualRules.MaximumTolerance);
    }

    public static int MoveOnePointToward(int current, int target)
    {
        if (current is < ManualRules.MinimumTolerance or > ManualRules.MaximumTolerance)
            throw new ArgumentOutOfRangeException(nameof(current));
        if (target is < ManualRules.MinimumTolerance or > ManualRules.MaximumTolerance)
            throw new ArgumentOutOfRangeException(nameof(target));
        return current == target ? current : current + Math.Sign(target - current);
    }

    internal static void ResolveUpkeep(MatchState state)
    {
        foreach (var sector in state.Sectors.OrderBy(sector => sector.Id))
            sector.Tolerance = MoveOnePointToward(sector.Tolerance, NormalTolerance(state, sector));
    }
}
