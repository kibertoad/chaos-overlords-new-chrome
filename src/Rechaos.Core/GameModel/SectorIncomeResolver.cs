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
}
