namespace Rechaos.Core.GameModel;

internal static class SectorControlResolver
{
    /// <summary>
    /// Makes the sector neutral and sets its sites' progress back to nothing, as RULE-POLICE-002
    /// does on a third Crackdown. The values rebuilt from completed sites before planning, the
    /// Tolerance and the sector's Support, keep theirs until the next rebuild (FND-CHAOS-002).
    /// </summary>
    public static void Neutralize(MatchState state, MatchSectorState sector)
    {
        if (sector.Owner is { } previousOwner)
            ResetInfluencedSites(state, sector, previousOwner);
        else
            ResetSiteProgress(state, sector);
        sector.Owner = null;
    }

    /// <summary>Sets the progress of every site of the sector back to nothing.</summary>
    public static void ResetSiteProgress(MatchState state, MatchSectorState sector)
    {
        foreach (var site in sector.Sites)
        {
            site.InfluencedBy = null;
            site.Resistance = state.Definitions.Site(site.DefinitionId).Resistance;
        }
    }

    public static void ResetInfluencedSites(
        MatchState state,
        MatchSectorState sector,
        PlayerId previousOwner)
    {
        var player = state.FindPlayer(previousOwner)!;
        foreach (var site in sector.Sites)
        {
            var definition = state.Definitions.Site(site.DefinitionId);
            if (site.InfluencedBy == previousOwner) player.Support -= definition.Support;
            // The sector's Tolerance drops the site's part at the next rebuild (RULE-SITE-001).
            site.InfluencedBy = null;
            site.Resistance = definition.Resistance;
        }
    }
}
