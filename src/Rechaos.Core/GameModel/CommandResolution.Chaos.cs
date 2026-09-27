namespace Rechaos.Core.GameModel;

/// <summary>
/// The Chaos pass: rolled at the end of Instant so the crackdowns it raises can take part in
/// Combat, paid out at the later Chaos boundary from the events that roll recorded.
/// </summary>
public static partial class CommandResolver
{
    private static IReadOnlyList<CommandResolutionResult> ResolveChaosPhase(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        var results = PreparedChaosResults(state, commands);
        if (results.Count == 0 && commands.Count > 0)
            results = PrepareChaosPhase(state, commands);
        // RULE-CHAOS-002: each player's surviving gangs' successes in a sector are added up and
        // halved once outside the player's own sectors, with ownership read now. A gang killed in
        // this turn's Combat is not paid [FND-CHAOS-002]; a sector that cracked down this turn
        // recorded its gangs' successes as 0.
        var totals = new SortedDictionary<(int Sector, int Player), int>();
        foreach (var result in results)
        {
            if (state.FindGang(result.Command.Gang) is not { IsActive: true } gang) continue;
            var key = (gang.SectorId, result.Command.Player.Value);
            totals[key] = totals.GetValueOrDefault(key) + result.Event!.Resolution!.PreviousValue.GetValueOrDefault();
        }
        foreach (var ((sectorId, playerValue), successes) in totals)
        {
            var player = state.FindPlayer(new PlayerId(playerValue))!;
            var payout = ManualRules.ChaosIncome(successes, state.Sectors[sectorId].Owner == player.Id);
            if (payout == 0) continue;
            player.Cash = checked(player.Cash + payout);
            player.Statistics.CashEarned += payout;
        }
        return results;
    }

    internal static IReadOnlyList<CommandResolutionResult> PrepareChaosPhase(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        var prepared = PreparedChaosResults(state, commands);
        if (prepared.Count > 0) return prepared;
        // RULE-CHAOS-001 tests every sector even when nobody ordered Chaos, so a sector whose
        // Tolerance is below 0 cracks down with no Chaos at all.
        // The original resolver records a per-player/sector presence byte from the
        // opening gang roster, then reports each Crackdown only to those present.
        // This includes occupants who did not submit a Chaos command. No instant action moves or
        // removes a gang, so the roster at the end of the instant phase is the one resolution
        // began with (RULE-EVENT-004).
        var crackdownObservers = state.Sectors.ToDictionary(
            sector => sector.Id,
            sector => state.Players
                .Where(player => player.Gangs.Any(gang => gang.IsActive && gang.SectorId == sector.Id))
                .Select(player => player.Id)
                .ToArray());
        var ordered = InRosterOrder(state, commands);
        var rolled = ordered.Select(queued =>
        {
            var gang = state.FindGang(queued.Command.Gang)!;
            var sector = state.Sectors[gang.SectorId];
            var band = OriginalResolutionRules.Band(state, queued.Command.Player);
            var pool = checked(sector.Income + gang.Force
                + EffectiveStatisticsCalculator.ForGang(state, gang).Chaos);
            var dice = OriginalResolutionRules.ActionPool(band, GangAction.Chaos, pool);
            var (rolls, successes) = RollAction(state, band, GangAction.Chaos, dice);
            return new ChaosRoll(queued, sector, rolls, successes, dice, band);
        }).ToArray();
        var groups = rolled
            .GroupBy(value => (value.Queued.Command.Player, value.Sector.Id))
            .Select(group =>
            {
                var values = group.ToArray();
                return new ChaosGroup(
                    values.Select(value => value.Queued).ToArray(), values[0].Sector,
                    values.SelectMany(value => value.Rolls).ToArray(),
                    values.Sum(value => value.Successes), values.Sum(value => value.DiceCount),
                    values[0].Band);
            })
            .ToArray();

        var sectorSuccesses = groups
            .GroupBy(group => group.Sector.Id)
            .ToDictionary(group => group.Key, group => group.Sum(value => value.Successes));
        var triggered = new Dictionary<int, CrackdownTriggerResult>();
        var crackdownChaos = groups
            .GroupBy(group => group.Sector.Id)
            .ToDictionary(group => group.Key, group => group.Sum(value =>
                OriginalResolutionRules.CrackdownContribution(
                    value.Band,
                    value.Sector.Owner == value.Participants[0].Command.Player,
                    value.Successes)));
        foreach (var sector in state.Sectors.OrderBy(value => value.Id))
        {
            if (!ManualRules.TriggersCrackdown(
                    crackdownChaos.GetValueOrDefault(sector.Id), sector.Tolerance)) continue;
            triggered.Add(sector.Id, CrackdownResolver.Trigger(
                state, sector, ExecutionPhase.Chaos, emitControlLostNotification: false));
        }

        var groupByCommand = groups.SelectMany(group => group.Participants.Select(
                participant => (participant.Sequence, Group: group)))
            .ToDictionary(value => value.Sequence, value => value.Group);
        // CashDelta records what the group is paid if every participant survives Combat; the
        // payout at the Chaos boundary pays only the survivors (RULE-CHAOS-002). Only a Crackdown
        // this turn stops the pay: police presence from earlier turns does not [FND-CHAOS-002].
        var payouts = new Dictionary<(PlayerId Player, int SectorId), int>();
        foreach (var group in groups)
        {
            var player = state.FindPlayer(group.Participants[0].Command.Player)!;
            var payout = triggered.ContainsKey(group.Sector.Id)
                ? 0
                : ManualRules.ChaosIncome(group.Successes, group.Sector.Owner == player.Id);
            payouts[(player.Id, group.Sector.Id)] = payout;
        }
        var ownSuccesses = rolled.ToDictionary(
            value => value.Queued.Sequence,
            value => triggered.ContainsKey(value.Sector.Id) ? 0 : value.Successes);

        var results = new List<CommandResolutionResult>(ordered.Length);
        var firstEventBySector = new Dictionary<int, long>();
        var paidGroups = new HashSet<(PlayerId Player, int SectorId)>();
        foreach (var participant in ordered)
        {
            var group = groupByCommand[participant.Sequence];
            var key = (participant.Command.Player, group.Sector.Id);
            var result = Complete(state, participant.Command, GameEventKind.CommandResolved,
                new CommandResolutionDetails(
                    CommandResolutionCode.Resolved, group.Rolls, group.Successes,
                    ownSuccesses[participant.Sequence], sectorSuccesses[group.Sector.Id],
                    CashDelta: paidGroups.Add(key) ? payouts[key] : 0,
                    AttackValue: group.DiceCount, DefenseValue: group.Sector.Tolerance),
                GameNotificationKind.Chaos,
                ExecutionPhase.Chaos);
            results.Add(result);
            firstEventBySector.TryAdd(group.Sector.Id, result.Event!.Sequence);
        }

        foreach (var sectorId in triggered.Keys.Order())
        {
            foreach (var playerId in crackdownObservers[sectorId])
                state.QueueNotification(
                    playerId, GameNotificationKind.Crackdown,
                    sectorId: sectorId,
                    relatedEventSequence: firstEventBySector.TryGetValue(sectorId, out var sequence) ? sequence : null,
                    executionPhase: ExecutionPhase.Chaos);
            var trigger = triggered[sectorId];
            if (trigger.ControlLost && trigger.PreviousOwner is { } previousOwner)
                state.QueueNotification(previousOwner, GameNotificationKind.ControlLost,
                    sectorId: sectorId, executionPhase: ExecutionPhase.Chaos);
        }
        return results;
    }

    /// <summary>
    /// Rebuilds the Chaos pass from the events the Instant boundary recorded.
    /// </summary>
    /// <remarks>
    /// Combat runs between the roll and the payout, and a Chaos participant killed there has its
    /// queue entry retired by <see cref="EliminateGang"/>. The recorded events are therefore the
    /// authoritative participant list: the queue can hold fewer gangs than rolled, never more. The
    /// dead gang's event stays in the pass, and the payout skips it because the gang is no longer
    /// active (RULE-CHAOS-002). Each event records the gang's own successes as
    /// <see cref="CommandResolutionDetails.PreviousValue"/>, 0 in a sector that cracked down.
    /// Event order is the roll order (player, then roster slot).
    /// </remarks>
    private static IReadOnlyList<CommandResolutionResult> PreparedChaosResults(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        var events = RolledThisTurn(state);
        if (events.Length == 0) return [];

        var queued = commands.ToDictionary(
            command => command.Command.Gang, command => command.Command);
        var rolled = events.Select(gameEvent => gameEvent.Gang!.Value).ToHashSet();
        if (queued.Keys.Any(gang => !rolled.Contains(gang)))
            throw new InvalidOperationException("The prepared Chaos pass does not match the command queue.");

        return events
            .Select(gameEvent =>
            {
                // A gang that survived keeps its queue entry, which carries Repeat. One that died in
                // Combat no longer has an entry, so the command is rebuilt from the event.
                var command = queued.GetValueOrDefault(gameEvent.Gang!.Value)
                    ?? new GameCommand(
                        gameEvent.Player, gameEvent.Gang!.Value, GangAction.Chaos, gameEvent.Target);
                return new CommandResolutionResult(command, gameEvent.Resolution!.Code, gameEvent);
            })
            .ToArray();
    }

    /// <summary>The Chaos rolls the Instant boundary recorded this turn, in roll order.</summary>
    /// <remarks>
    /// Walked back from the end rather than filtered, the way every other reader of the log does
    /// it: the history is append-only in sequence order, so the open turn's tail is the whole
    /// answer and the ascending order the pass needs falls out of reversing the walk. Filtering all
    /// of it made the Chaos subphase cost grow with every turn already played.
    /// </remarks>
    private static GameEvent[] RolledThisTurn(MatchState state)
    {
        var events = state.Events;
        var rolled = new List<GameEvent>();
        for (var index = events.Count - 1; index >= 0; index--)
        {
            var gameEvent = events[index];
            if (gameEvent.Turn != state.Coordinator.Turn) break;
            if (gameEvent.Phase == TurnPhase.Execution
                && gameEvent.ExecutionPhase == ExecutionPhase.Chaos
                && gameEvent.Action == GangAction.Chaos
                && gameEvent.Kind is GameEventKind.CommandResolved or GameEventKind.CommandFailed)
                rolled.Add(gameEvent);
        }
        rolled.Reverse();
        return rolled.ToArray();
    }

    private sealed record ChaosRoll(
        QueuedCommand Queued,
        MatchSectorState Sector,
        IReadOnlyList<int> Rolls,
        int Successes,
        int DiceCount,
        OriginalResolutionBand Band);

    private sealed record ChaosGroup(
        IReadOnlyList<QueuedCommand> Participants,
        MatchSectorState Sector,
        IReadOnlyList<int> Rolls,
        int Successes,
        int DiceCount,
        OriginalResolutionBand Band);
}
