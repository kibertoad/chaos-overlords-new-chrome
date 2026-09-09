namespace Rechaos.Core.GameModel;

/// <summary>Manual-backed police duration and turn-boundary state.</summary>
public sealed record CrackdownTriggerResult(
    int Duration,
    PlayerId? PreviousOwner,
    bool ControlLost);

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

    public static CrackdownTriggerResult Trigger(MatchState state, MatchSectorState sector)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        if (state.Sectors.Count <= sector.Id || state.Sectors[sector.Id] != sector)
            throw new ArgumentException("Sector does not belong to the match.", nameof(sector));

        var duration = state.Random.NextInclusive(
            ManualRules.MaximumCrackdownTurns - ManualRules.MinimumCrackdownTurns + 1)
            + ManualRules.MinimumCrackdownTurns - 1;
        sector.CrackdownTurnsRemaining = checked(sector.CrackdownTurnsRemaining + duration);
        var previousOwner = sector.Owner;
        var controlLost = sector.RecordCrackdown(state.Coordinator.Turn) && previousOwner is not null;
        if (controlLost) SectorControlResolver.Neutralize(state, sector);
        return new CrackdownTriggerResult(duration, previousOwner, controlLost);
    }
}
