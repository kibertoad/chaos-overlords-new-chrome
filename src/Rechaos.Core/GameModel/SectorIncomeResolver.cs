namespace Rechaos.Core.GameModel;

/// <summary>
/// Rebuilds the owner-only sector Cash byte used by Upkeep and the city UI.
/// This is distinct from the generated 3-7 <see cref="MatchSectorState.Income"/>
/// value consumed by Chaos and Control.
/// </summary>
public static class SectorIncomeResolver
{
    public static int SectorCash(MatchState state, MatchSectorState sector)
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
