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
    }

    public static void FinishCombat(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var sector in state.Sectors.OrderBy(sector => sector.Id))
            if (sector.CrackdownTurnsRemaining > 0)
                sector.CrackdownTurnsRemaining--;
    }

    public static CrackdownTriggerResult Trigger(
        MatchState state,
        MatchSectorState sector,
        ExecutionPhase? notificationPhase = null,
        bool emitControlLostNotification = true)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(sector);
        state.RequireSector(sector);

        var previousOwner = sector.Owner;
        var controlLost = sector.RecordCrackdown(state.Coordinator.Turn) && previousOwner is not null;
        if (controlLost)
        {
            SectorControlResolver.Neutralize(state, sector);
            if (emitControlLostNotification)
                state.QueueNotification(
                    previousOwner!.Value,
                    GameNotificationKind.ControlLost,
                    sectorId: sector.Id,
                    executionPhase: notificationPhase);
        }
        var duration = state.Random.NextInclusive(
            ManualRules.MaximumCrackdownTurns - ManualRules.MinimumCrackdownTurns + 1)
            + ManualRules.MinimumCrackdownTurns - 1;
        sector.CrackdownTurnsRemaining = checked(sector.CrackdownTurnsRemaining + duration);
        return new CrackdownTriggerResult(duration, previousOwner, controlLost);
    }
}
