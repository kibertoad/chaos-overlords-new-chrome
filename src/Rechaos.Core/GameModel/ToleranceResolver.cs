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
        state.RequireSector(sector);

        var siteAdjustment = SiteAdjustment(state, sector);
        return checked(IncomeToleranceSum - sector.Income + siteAdjustment);
    }

    public static int SiteAdjustment(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        return sector.Sites
            .Where(site => SiteControlRules.Controller(sector, site) is not null)
            .Sum(site => state.Definitions.Site(site.DefinitionId).Tolerance);
    }

    public static int ApplyBribe(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        return checked(sector.Tolerance + ManualRules.BribeToleranceIncrease);
    }

    public static int ApplySnitch(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        return checked(sector.Tolerance - ManualRules.SnitchToleranceDecrease);
    }

    internal static void ClampAfterInstant(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var sector in state.Sectors)
            if (sector.Tolerance < 1) sector.Tolerance = 1;
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
