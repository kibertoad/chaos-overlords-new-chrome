using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

/// <summary>
/// The sector fields RULE-SITE-001 rebuilds from the completed sites, all from one loop and one
/// completion test (<see cref="SiteControlRules.IsComplete"/>). Every sum is stored as a signed
/// byte, as the original's record holds it (FMT-STATE-002).
/// </summary>
public readonly record struct SectorSiteTotals(
    int CashYield, int Tolerance, int Support, int ResearchLevel);

public static class SectorRecordRebuild
{
    /// <summary>
    /// RULE-SITE-001: <c>cash_yield</c> is 1 plus the Cash of each completed site,
    /// <c>tolerance</c> the base plus their Tolerance, <c>support</c> their Support, and
    /// <c>research_level</c> 2 with a completed Research Lab, else 1 with a Science Center, else
    /// 0. The sector's owner plays no part.
    /// </summary>
    public static SectorSiteTotals Rebuilt(OriginalData definitions, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(sector);
        var cashYield = ManualRules.ControlledSectorTax;
        var tolerance = sector.BaseTolerance;
        var support = 0;
        var researchLevel = 0;
        foreach (var site in sector.Sites)
        {
            var definition = definitions.Site(site.DefinitionId);
            if (!SiteControlRules.IsComplete(site, definition)) continue;
            cashYield = SignedByte(cashYield + definition.Cash);
            tolerance = SignedByte(tolerance + definition.Tolerance);
            support = SignedByte(support + definition.Support);
            researchLevel = SpecialSiteRules.RaiseResearchLevel(researchLevel, definition.Special);
        }
        return new SectorSiteTotals(cashYield, tolerance, support, researchLevel);
    }

    /// <summary>
    /// RULE-SITE-001: before planning, after Upkeep has paid the previous <c>cash_yield</c>
    /// (RULE-UPKEEP-001), every sector's stored fields are rewritten. The headquarters site is
    /// complete at progress 0 and adds its Tolerance whoever owns the sector, neutral included
    /// (RULE-CHAOS-001). <c>research_level</c> is not stored: the computer reads it only while
    /// planning (RULE-AI-026), when <see cref="Rebuilt"/> gives the value this rebuild wrote.
    /// </summary>
    internal static void BeforePlanning(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var sector in state.Sectors)
        {
            var totals = Rebuilt(state.Definitions, sector);
            sector.CashYield = totals.CashYield;
            sector.Tolerance = totals.Tolerance;
            sector.Support = totals.Support;
        }
    }

    private static int SignedByte(int value) => unchecked((sbyte)value);
}
