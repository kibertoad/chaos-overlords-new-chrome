namespace Rechaos.Core.GameModel;

/// <summary>
/// Rebuilds the sector Income byte used during playable turns. The original
/// city generator's 3-7 value is retained on <see cref="MatchSectorState"/> as
/// the density-derived tolerance baseline; before planning, the executable
/// replaces the operational Income byte with one dollar of sector tax plus
/// the Cash values of completed sites.
/// </summary>
public static class SectorIncomeResolver
{
    public static int OperationalIncome(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        if (sector.Id < 0 || sector.Id >= state.Sectors.Count || state.Sectors[sector.Id] != sector)
            throw new ArgumentException("Sector does not belong to the match.", nameof(sector));

        return checked(ManualRules.ControlledSectorTax + sector.Sites
            .Where(site => site.InfluencedBy is not null)
            .Sum(site => state.Definitions.Sites.Single(
                definition => definition.Id == site.DefinitionId).Cash));
    }
}
