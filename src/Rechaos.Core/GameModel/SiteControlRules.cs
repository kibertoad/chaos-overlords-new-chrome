using Rechaos.Core.Assets;

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

    /// <summary>
    /// RULE-SITE-001: whether the rebuild before planning counts the site, which it does when the
    /// progress has reached the definition's Resistance. The sector's owner plays no part. A site
    /// whose definition has Resistance 0 (the headquarters) is complete at progress 0, so it
    /// counts in a neutral sector too. Any other site counts once the rebuild has activated it for
    /// the owner, since every change of owner resets its progress.
    /// </summary>
    public static bool IsComplete(MatchSiteState site, SiteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(definition);
        return site.InfluencedBy is not null || definition.Resistance == 0;
    }
}
