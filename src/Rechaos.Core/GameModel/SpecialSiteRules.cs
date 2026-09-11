using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public static class SpecialSiteRules
{
    public const short ScienceCenter = 1;
    public const short ResearchLab = 2;
    public const short Factory = 3;
    public const int BaseResearchTechLimit = 5;
    public const int ScienceCenterTechLimit = 8;
    public const int ResearchLabTechLimit = 10;
    public const int FactoryDiscountDivisor = 3;

    public static int ResearchTechLimit(MatchState state, MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var player = state.FindPlayer(gang.Owner)
            ?? throw new ArgumentException("Gang owner does not belong to the match.", nameof(gang));
        if (!player.Gangs.Contains(gang))
            throw new ArgumentException("Gang does not belong to the match.", nameof(gang));
        var siteLimit = BaseResearchTechLimit;
        foreach (var site in InfluencedLocalSpecials(state, gang))
        {
            siteLimit = site.Special switch
            {
                ResearchLab => Math.Max(siteLimit, ResearchLabTechLimit),
                ScienceCenter => Math.Max(siteLimit, ScienceCenterTechLimit),
                _ => siteLimit
            };
        }
        var gangTech = state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId).TechLevel;
        return Math.Min(gangTech, siteLimit);
    }

    public static int EquipmentCost(MatchState state, MatchGangState gang, ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        ArgumentNullException.ThrowIfNull(item);
        if (item.Cost < 0) throw new ArgumentOutOfRangeException(nameof(item));
        var hasFactory = InfluencedLocalSpecials(state, gang).Any(site => site.Special == Factory);
        return hasFactory ? item.Cost - item.Cost / FactoryDiscountDivisor : item.Cost;
    }

    private static IEnumerable<SiteDefinition> InfluencedLocalSpecials(
        MatchState state,
        MatchGangState gang)
    {
        var sector = state.Sectors[gang.SectorId];
        if (sector.Owner != gang.Owner) yield break;
        foreach (var site in sector.Sites.Where(site => site.InfluencedBy == gang.Owner))
            yield return state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
    }
}
