namespace Rechaos.Core.GameModel;

/// <summary>Manual-backed police duration and turn-boundary state.</summary>
public static class CrackdownResolver
{
    public static void ResolveUpkeep(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var sector in state.Sectors.OrderBy(sector => sector.Id))
        {
            sector.Chaos = 0;
            if (sector.CrackdownTurnsRemaining > 0)
                sector.CrackdownTurnsRemaining--;
        }
    }

    public static int Trigger(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        if (state.Sectors.Count <= sector.Id || state.Sectors[sector.Id] != sector)
            throw new ArgumentException("Sector does not belong to the match.", nameof(sector));

        var duration = state.Random.NextInclusive(
            ManualRules.MaximumCrackdownTurns - ManualRules.MinimumCrackdownTurns + 1)
            + ManualRules.MinimumCrackdownTurns - 1;
        sector.CrackdownTurnsRemaining = checked(sector.CrackdownTurnsRemaining + duration);
        return duration;
    }
}
