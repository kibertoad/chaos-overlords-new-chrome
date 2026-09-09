namespace Rechaos.Core.GameModel;

internal static class SectorControlResolver
{
    public static void Neutralize(MatchState state, MatchSectorState sector)
    {
        if (sector.Owner is not { } previousOwner) return;
        ResetInfluencedSites(state, sector, previousOwner);
        sector.Owner = null;
    }

    public static void ResetInfluencedSites(
        MatchState state,
        MatchSectorState sector,
        PlayerId previousOwner)
    {
        var player = state.FindPlayer(previousOwner)!;
        foreach (var site in sector.Sites)
        {
            var definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
            if (site.InfluencedBy == previousOwner) player.Support -= definition.Support;
            if (site.InfluencedBy is not null)
                sector.Tolerance = checked(sector.Tolerance - definition.Tolerance);
            site.InfluencedBy = null;
            site.Resistance = definition.Resistance;
        }
    }
}
