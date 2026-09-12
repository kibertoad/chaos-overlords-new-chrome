namespace Rechaos.Core.GameModel;

public static class SiteControlRules
{
    public static PlayerId? Controller(MatchSectorState sector, MatchSiteState site)
    {
        ArgumentNullException.ThrowIfNull(sector);
        ArgumentNullException.ThrowIfNull(site);
        return site.InfluencedBy
            ?? (site.DefinitionId == MatchBootstrap.HeadquartersDefinitionId
                && site.Resistance == 0
                    ? sector.Owner
                    : null);
    }
}
