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

    /// <summary>
    /// The Tech limit the Research list offers a gang (SCR-RESEARCH-001, FND-RESEARCH-003): the
    /// research level of the specials the player has influenced in its own sector.
    /// </summary>
    public static int ResearchTechLimit(MatchState state, MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var player = state.FindPlayer(gang.Owner)
            ?? throw new ArgumentException("Gang owner does not belong to the match.", nameof(gang));
        if (!player.Gangs.Contains(gang))
            throw new ArgumentException("Gang does not belong to the match.", nameof(gang));
        var level = 0;
        foreach (var site in InfluencedLocalSpecials(state, gang))
            level = RaiseResearchLevel(level, site.Special);
        return LocalTechLimit(state, gang, level);
    }

    /// <summary>
    /// The <c>local_tech_cap</c> the computer players' item choices apply (RULE-AI-005,
    /// RULE-AI-026): the gang definition's Tech Level, lowered to 5, 8 or 10 by the
    /// <c>research_level</c> of the gang's sector (FMT-STATE-002), whoever owns the sector
    /// (FND-AI-054). The Research list a human sees reads that level only in the player's own
    /// sector (SCR-RESEARCH-001, FND-RESEARCH-003), which <see cref="ResearchTechLimit"/> does.
    /// </summary>
    public static int ComputerTechLimit(MatchState state, MatchGangState gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(gang);
        var sector = state.Sectors[gang.SectorId];
        return LocalTechLimit(
            state, gang, SectorRecordRebuild.Rebuilt(state.Definitions, sector).ResearchLevel);
    }

    /// <summary>
    /// RULE-SITE-001 <c>research_level</c>: 2 once a Research Lab counts, else 1 once a Science
    /// Center does, else 0.
    /// </summary>
    internal static int RaiseResearchLevel(int level, short special) => special switch
    {
        ResearchLab => Math.Max(level, 2),
        ScienceCenter => Math.Max(level, 1),
        _ => level
    };

    // The gang's Tech Level, lowered to the limit the research level allows.
    private static int LocalTechLimit(MatchState state, MatchGangState gang, int researchLevel) =>
        Math.Min((int)state.Definitions.Gang(gang.DefinitionId).TechLevel, researchLevel switch
        {
            0 => BaseResearchTechLimit,
            1 => ScienceCenterTechLimit,
            _ => ResearchLabTechLimit
        });

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
            yield return state.Definitions.Site(site.DefinitionId);
    }
}
