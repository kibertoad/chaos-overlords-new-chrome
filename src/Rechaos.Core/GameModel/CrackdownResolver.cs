namespace Rechaos.Core.GameModel;

/// <summary>Manual-backed police duration and turn-boundary state.</summary>
public sealed record CrackdownTriggerResult(
    int Duration,
    PlayerId? PreviousOwner,
    bool ControlLost);

public static class CrackdownResolver
{
    /// <summary>
    /// The police presence that never counts down, which the island name rule writes into every
    /// neutral sector (RULE-SETUP-005, RULE-POLICE-003).
    /// </summary>
    public const int PermanentCrackdownTurns = 100;

    public static void ResolveUpkeep(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
    }

    /// <summary>
    /// RULE-POLICE-003: after the combat phase every presence from 1 to 99 loses a turn. A value
    /// of 100 or more stays, so the island name's police never leave, and a Crackdown that adds
    /// its 3 to 5 turns to them keeps them permanent. A value wrapped below 0 stays too.
    /// </summary>
    public static void FinishCombat(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var sector in state.Sectors.OrderBy(sector => sector.Id))
            if (sector.CrackdownTurnsRemaining is > 0 and < PermanentCrackdownTurns)
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

        // RULE-POLICE-002: a Crackdown fills a free slot of the five-turn history and brings no
        // police. The third within the window neutralizes the sector, clears its sites' progress
        // and only then adds 3 to 5 turns of police, drawn after the history update
        // (FND-POLICE-004). A neutral sector is neutralized again and gets the police too.
        var previousOwner = sector.Owner;
        if (!sector.RecordCrackdown(state.Coordinator.Turn))
            return new CrackdownTriggerResult(0, previousOwner, ControlLost: false);

        var controlLost = previousOwner is not null;
        SectorControlResolver.Neutralize(state, sector);
        if (controlLost && emitControlLostNotification)
            state.QueueNotification(
                previousOwner!.Value,
                GameNotificationKind.ControlLost,
                sectorId: sector.Id,
                executionPhase: notificationPhase);
        var duration = state.Random.NextInclusive(
            ManualRules.MaximumCrackdownTurns - ManualRules.MinimumCrackdownTurns + 1)
            + ManualRules.MinimumCrackdownTurns - 1;
        // FMT-STATE-002 holds `crackdown_turns` in a signed byte. From 100 the countdown of
        // RULE-POLICE-003 no longer runs, so repeated neutralizations wrap it past 127 to a
        // negative value, which the police phase reads as no police and the Control pass and the
        // computer players read as police (RULE-POLICE-001, RULE-CONTROL-001, RULE-AI-004).
        sector.CrackdownTurnsRemaining = unchecked((sbyte)(sector.CrackdownTurnsRemaining + duration));
        return new CrackdownTriggerResult(duration, previousOwner, controlLost);
    }
}
