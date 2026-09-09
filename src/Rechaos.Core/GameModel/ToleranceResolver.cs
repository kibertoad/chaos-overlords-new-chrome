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

        var siteAdjustment = SiteAdjustment(state, sector);
        return checked(IncomeToleranceSum - sector.Income + siteAdjustment);
    }

    public static int SiteAdjustment(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        return sector.Sites
            .Where(site => site.InfluencedBy is not null)
            .Sum(site => state.Definitions.Sites.Single(
                definition => definition.Id == site.DefinitionId).Tolerance);
    }

    public static int ApplyBribe(MatchState state, MatchSectorState sector)
    {
        var adjustment = SiteAdjustment(state, sector);
        return checked(ManualRules.ApplyBribe(sector.Tolerance - adjustment) + adjustment);
    }

    public static int ApplySnitch(MatchState state, MatchSectorState sector)
    {
        var adjustment = SiteAdjustment(state, sector);
        return checked(ManualRules.ApplySnitch(sector.Tolerance - adjustment) + adjustment);
    }

    public static int MoveOnePointToward(int current, int target)
    {
        return current == target ? current : current + Math.Sign(target - current);
    }

    internal static void ResolveUpkeep(MatchState state)
    {
        foreach (var sector in state.Sectors.OrderBy(sector => sector.Id))
            sector.Tolerance = MoveOnePointToward(sector.Tolerance, NormalTolerance(state, sector));
    }
}
