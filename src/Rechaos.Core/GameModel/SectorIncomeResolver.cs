using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

/// <summary>
/// The owner-only sector Cash byte used by Upkeep and the city UI (FMT-STATE-002 `cash_yield`).
/// This is distinct from the generated 3-7 <see cref="MatchSectorState.Income"/>
/// value consumed by Chaos and Control.
/// </summary>
public static class SectorIncomeResolver
{
    /// <summary>
    /// RULE-UPKEEP-001: what the sector pays whoever owns it at Upkeep, the value of the last
    /// rebuild. After a takeover during resolution it still holds the Cash of the sites whose
    /// progress the takeover reset.
    /// </summary>
    public static int SectorCash(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        state.RequireSector(sector);
        return sector.CashYield;
    }

    /// <summary>
    /// RULE-SITE-001: 1 plus the Cash of each completed site, each sum stored as a signed byte.
    /// </summary>
    public static int Rebuilt(OriginalData definitions, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(sector);
        var cashYield = ManualRules.ControlledSectorTax;
        foreach (var site in sector.Sites)
        {
            var definition = definitions.Site(site.DefinitionId);
            if (SiteControlRules.IsComplete(site, definition))
                cashYield = unchecked((sbyte)(cashYield + definition.Cash));
        }
        return cashYield;
    }

    /// <summary>
    /// RULE-SITE-001: before planning, after Upkeep has paid the previous value
    /// (RULE-UPKEEP-001), every sector's Cash yield is rebuilt from its completed sites.
    /// </summary>
    internal static void RebuildBeforePlanning(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var sector in state.Sectors)
            sector.CashYield = Rebuilt(state.Definitions, sector);
    }
}
