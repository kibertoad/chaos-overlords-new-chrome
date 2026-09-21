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
        foreach (var result in results)
        {
            var payout = result.Event!.Resolution!.CashDelta;
            if (payout == 0) continue;
            var player = state.FindPlayer(result.Command.Player)!;
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
        if (prepared.Count > 0 || commands.Count == 0) return prepared;
        var ordered = commands
            .OrderBy(queued => queued.Command.Player.Value)
            .ThenBy(queued => GangSlot(state, queued.Command))
            .ToArray();
        var rolled = ordered.Select(queued =>
        {
            var gang = state.FindGang(queued.Command.Gang)!;
            var sector = state.Sectors[gang.SectorId];
            var band = OriginalResolutionRules.Band(state, queued.Command.Player);
            var pool = checked(sector.Income + gang.Force
                + EffectiveStatisticsCalculator.ForGang(state, gang).Chaos);
            var dice = OriginalResolutionRules.ActionPool(band, GangAction.Chaos, pool);
            var rolls = DiceRoller.RollD6(state.Random, dice);
            var successes = OriginalResolutionRules.CountSuccesses(
                rolls, OriginalResolutionRules.SuccessThreshold(band, GangAction.Chaos));
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
        var triggered = new HashSet<int>();
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
            CrackdownResolver.Trigger(state, sector, ExecutionPhase.Chaos);
            triggered.Add(sector.Id);
        }

        var groupByCommand = groups.SelectMany(group => group.Participants.Select(
                participant => (participant.Sequence, Group: group)))
            .ToDictionary(value => value.Sequence, value => value.Group);
        var payouts = new Dictionary<(PlayerId Player, int SectorId), int>();
        foreach (var group in groups)
        {
            var player = state.FindPlayer(group.Participants[0].Command.Player)!;
            var payout = group.Sector.CrackdownActive
                ? 0
                : ManualRules.ChaosIncome(group.Successes, group.Sector.Owner == player.Id);
            payouts[(player.Id, group.Sector.Id)] = payout;
        }

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
                    0, sectorSuccesses[group.Sector.Id],
                    CashDelta: paidGroups.Add(key) ? payouts[key] : 0,
                    AttackValue: group.DiceCount, DefenseValue: group.Sector.Tolerance),
                GameNotificationKind.Chaos,
                ExecutionPhase.Chaos);
            results.Add(result);
            firstEventBySector.TryAdd(group.Sector.Id, result.Event!.Sequence);
        }

        foreach (var sectorId in triggered.Order())
        {
            foreach (var player in state.Players.Where(player => player.Status == PlayerStatus.Active))
                state.QueueNotification(
                    player.Id, GameNotificationKind.Crackdown,
                    sectorId: sectorId,
                    relatedEventSequence: firstEventBySector.TryGetValue(sectorId, out var sequence) ? sequence : null,
                    executionPhase: ExecutionPhase.Chaos);
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
    /// dead gang's event stays in the pass because the group payout is written as
    /// <see cref="CommandResolutionDetails.CashDelta"/> on the first participant of each
    /// (player, sector) group; dropping it would take the surviving participants' income with it.
    /// Event order is the roll order (player, then roster slot), so the pass is unchanged for a
    /// turn where nobody died.
    /// </remarks>
    private static IReadOnlyList<CommandResolutionResult> PreparedChaosResults(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        var events = state.Events.Where(gameEvent =>
                gameEvent.Turn == state.Coordinator.Turn
                && gameEvent.Phase == TurnPhase.Execution
                && gameEvent.ExecutionPhase == ExecutionPhase.Chaos
                && gameEvent.Action == GangAction.Chaos
                && gameEvent.Kind is GameEventKind.CommandResolved or GameEventKind.CommandFailed)
            .OrderBy(gameEvent => gameEvent.Sequence)
            .ToArray();
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
