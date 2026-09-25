namespace Rechaos.Core.GameModel;

public enum CommandResolutionCode : byte
{
    Resolved,
    InsufficientCash,
    UnsupportedAction,
    ItemUnavailable,
    DestinationFull,
    TargetHidden,
    TargetEvaded,
    SectorInCrackdown
}

public sealed record CommandResolutionResult(
    GameCommand Command,
    CommandResolutionCode Code,
    GameEvent? Event)
{
    public bool Succeeded => Code == CommandResolutionCode.Resolved;
}

public sealed record PoliceAttackResolutionResult(
    GangId Gang,
    PlayerId Owner,
    PoliceAttackResolutionDetails Details,
    GameEvent Event);

public sealed record CombatPhaseResolution(
    IReadOnlyList<CommandResolutionResult> Commands,
    IReadOnlyList<PoliceAttackResolutionResult> PoliceAttacks);

/// <summary>
/// Deterministic action dispatch. Only actions backed by recorded evidence are
/// enabled; unsupported actions are rejected before a subphase mutates state.
/// </summary>
public static partial class CommandResolver
{
    public static bool IsSupported(GangAction action) =>
        action is GangAction.Attack or GangAction.Bribe or GangAction.Chaos or GangAction.Equip or GangAction.Give or GangAction.Heal
            or GangAction.Hide or GangAction.Influence or GangAction.Move or GangAction.Research
            or GangAction.Sell or GangAction.Snitch or GangAction.Terminate or GangAction.Control;

    public static IReadOnlyList<CommandResolutionResult> ResolvePhase(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(commands);
        if (state.Coordinator.Phase != TurnPhase.Execution || state.Coordinator.ExecutionPhase is not { } phase)
            throw new InvalidOperationException("A command phase can only resolve during Execution.");
        if (commands.Any(command => command.ExecutionPhase != phase))
            throw new ArgumentException("Every command must belong to the current execution subphase.", nameof(commands));
        if (phase == ExecutionPhase.Instant) return ResolveInstantPhase(state, commands);
        if (phase == ExecutionPhase.Combat) return ResolveCombatPhase(state, commands).Commands;
        if (phase == ExecutionPhase.Chaos) return ResolveChaosPhase(state, commands);
        if (phase == ExecutionPhase.Control) return ResolveControlPhase(state, commands);
        if (phase == ExecutionPhase.Movement) return ResolveMovementPhase(state, commands);
        if (phase == ExecutionPhase.Transaction) return ResolveTransactionPhase(state, commands);
        return commands.Select(queued => Resolve(state, queued)).ToArray();
    }

    private static IReadOnlyList<CommandResolutionResult> ResolveInstantPhase(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        // RULE-TOLERANCE-001: resolution opens by moving every base Tolerance one point back.
        ToleranceResolver.StepTowardNormal(state);
        var ordered = InRosterOrder(state, commands);
        var results = new List<CommandResolutionResult>(ordered.Length);
        foreach (var queued in ordered) results.Add(Resolve(state, queued));
        ToleranceResolver.ClampAfterInstant(state);
        return results;
    }

    public static CommandResolutionResult Resolve(MatchState state, QueuedCommand queued)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(queued);
        if (state.Coordinator.Phase != TurnPhase.Execution
            || state.Coordinator.ExecutionPhase != queued.ExecutionPhase)
            throw new InvalidOperationException("A command can only resolve during its execution subphase.");

        return queued.Command.Action switch
        {
            GangAction.Attack => ResolveCombatPhase(state, [queued]).Commands.Single(),
            GangAction.Bribe => ResolveBribe(state, queued.Command),
            GangAction.Chaos => ResolveChaosPhase(state, [queued]).Single(),
            GangAction.Control => ResolveControlPhase(state, [queued]).Single(),
            GangAction.Equip => ResolveEquip(state, queued.Command),
            GangAction.Give => ResolveGive(state, queued.Command),
            GangAction.Heal => ResolveHeal(state, queued.Command),
            GangAction.Hide => ResolveHide(state, queued.Command),
            GangAction.Influence => ResolveInfluence(state, queued.Command),
            GangAction.Move => ResolveMove(state, queued.Command),
            GangAction.Research => ResolveResearch(state, queued.Command),
            GangAction.Sell => ResolveSell(state, queued.Command),
            GangAction.Snitch => ResolveSnitch(state, queued.Command),
            GangAction.Terminate => ResolveTerminate(state, queued.Command),
            _ => new CommandResolutionResult(queued.Command, CommandResolutionCode.UnsupportedAction, null)
        };
    }

    private static CommandResolutionResult ResolveBribe(MatchState state, GameCommand command)
    {
        var player = state.FindPlayer(command.Player)!;
        var cost = CommandRules.ByAction[GangAction.Bribe].CashCost;
        if (player.Cash < cost)
        {
            var tolerance = state.Sectors[state.FindGang(command.Gang)!.SectorId].BaseTolerance;
            return Complete(state, command, GameEventKind.CommandFailed,
                new CommandResolutionDetails(CommandResolutionCode.InsufficientCash, [], 0, tolerance, tolerance));
        }

        var gang = state.FindGang(command.Gang)!;
        var sector = state.Sectors[gang.SectorId];
        player.Cash -= cost;
        player.Statistics.CashSpent += cost;
        var before = sector.BaseTolerance;
        var after = ToleranceResolver.ApplyBribe(state, sector);
        sector.BaseTolerance = after;
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before, after, -cost));
    }

    public static CombatPhaseResolution ResolveCombatPhase(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(commands);
        if (state.Coordinator.Phase != TurnPhase.Execution
            || state.Coordinator.ExecutionPhase != ExecutionPhase.Combat)
            throw new InvalidOperationException("Police and gang combat can only resolve during Combat.");
        if (commands.Any(command => command.ExecutionPhase != ExecutionPhase.Combat))
            throw new ArgumentException("Every command must belong to Combat.", nameof(commands));

        var orderedCommands = InRosterOrder(state, commands);
        var snapshots = state.Players.SelectMany(player => player.Gangs)
            .ToDictionary(gang => gang.Id, gang => CombatSnapshot.For(state, gang));
        var outcomes = new List<CombatOutcome>(orderedCommands.Length);
        // Every Attack order rolls its own attack and retaliation, including two gangs that attack
        // each other: the original resolver has no branch that merges such a pair
        // (RULE-ATTACK-001, FND-COMBAT-006).
        foreach (var queued in orderedCommands)
        {
            var attacker = snapshots[queued.Command.Gang];
            var target = snapshots[new GangId(queued.Command.Target.Id)];
            int? detectionRoll = null;
            int? detectionChance = null;
            if (target.Hidden)
            {
                var evasionThreshold = OriginalResolutionRules.HiddenEvasionThreshold(
                    OriginalResolutionRules.Band(state, attacker.Owner),
                    attacker.Statistics.Detect,
                    target.Statistics.Stealth);
                detectionChance = OriginalResolutionRules.HiddenHitPercent(evasionThreshold);
                detectionRoll = state.Random.NextInclusive(20);
                if (detectionRoll < evasionThreshold)
                {
                    outcomes.Add(new CombatOutcome(queued, attacker, target, CommandResolutionCode.TargetEvaded,
                        [], 0, 0, [], 0, 0, detectionRoll, detectionChance));
                    continue;
                }
            }

            // RULE-COMBAT-001: the stored Combat already holds the weapon skills.
            var attackDice = ManualRules.AttackDiceCount(
                attacker.Force,
                attacker.Statistics.Combat,
                OriginalResolutionRules.AdjustDefense(
                    OriginalResolutionRules.Band(state, target.Owner),
                    target.Statistics.Defense));
            var attackRolls = DiceRoller.RollD6(state.Random, attackDice);
            var attackSuccesses = OriginalResolutionRules.CountSuccesses(
                attackRolls,
                OriginalResolutionRules.SuccessThreshold(
                    OriginalResolutionRules.Band(state, attacker.Owner), GangAction.Attack));
            var attackDamage = OriginalResolutionRules.MainAttackDamage(
                attackDice, attackSuccesses);
            var suppressesRetaliation = target.Hidden
                || ManualRules.SuppressesRetaliation(
                    attacker.Statistics, attacker.WeaponType, target.Statistics, target.WeaponType);
            var retaliationDice = suppressesRetaliation
                ? 0
                : ManualRules.AttackDiceCount(
                    target.Force,
                    target.Statistics.Combat,
                    attacker.Statistics.Defense);
            var retaliationRolls = DiceRoller.RollD6(state.Random, retaliationDice);
            var retaliationSuccesses = OriginalResolutionRules.CountSuccesses(
                retaliationRolls,
                OriginalResolutionRules.RetaliationThreshold(
                    OriginalResolutionRules.Band(state, target.Owner)));
            outcomes.Add(new CombatOutcome(
                queued, attacker, target, CommandResolutionCode.Resolved,
                attackRolls, attackSuccesses, attackDamage,
                retaliationRolls, retaliationSuccesses,
                ManualRules.RetaliationDamage(retaliationSuccesses),
                detectionRoll, detectionChance));
        }

        var policeOutcomes = snapshots.Values
            .Where(snapshot => snapshot.Force > 0 && state.Sectors[snapshot.SectorId].CrackdownActive)
            .OrderBy(snapshot => snapshot.Owner.Value)
            .ThenBy(snapshot => GangSlot(state, snapshot.Owner, snapshot.Id))
            .Select(snapshot => RollPoliceAttack(state, snapshot))
            .ToArray();

        // RULE-AI-016: every Attack order lowers the target player's attitude, an evaded one by the
        // reaction alone, since its opening damage is -1 [FND-AI-047].
        foreach (var outcome in outcomes)
            state.AiStrategy.RecordCombat(outcome.Attacker.Owner, outcome.Target.Owner,
                outcome.Code == CommandResolutionCode.Resolved ? outcome.Damage : -1);
        var incomingDamage = new Dictionary<GangId, int>();
        foreach (var outcome in outcomes.Where(outcome => outcome.Code == CommandResolutionCode.Resolved))
        {
            AddDamage(incomingDamage, outcome.Target.Id, outcome.Damage);
            AddDamage(incomingDamage, outcome.Attacker.Id, outcome.RetaliationDamage);
        }
        foreach (var outcome in policeOutcomes.Where(outcome => outcome.Detected))
            AddDamage(incomingDamage, outcome.Target.Id, outcome.Successes);

        foreach (var (gangId, damage) in incomingDamage)
        {
            var gang = state.FindGang(gangId)!;
            gang.Force = Math.Max(0, snapshots[gangId].Force - damage);
        }
        CreditCombatStatistics(state, outcomes);

        var results = new List<CommandResolutionResult>(commands.Count);
        var firstEventByGang = new Dictionary<GangId, long>();
        foreach (var outcome in outcomes)
        {
            var eventKind = outcome.Code == CommandResolutionCode.Resolved
                ? GameEventKind.CommandResolved
                : GameEventKind.CommandFailed;
            var result = Complete(state, outcome.Queued.Command, eventKind,
                new CommandResolutionDetails(
                    outcome.Code, outcome.AttackRolls, outcome.AttackSuccesses,
                    outcome.Target.Force, state.FindGang(outcome.Target.Id)!.Force,
                    ItemId: outcome.Attacker.WeaponItemId,
                    AttackValue: outcome.AttackRolls.Count,
                    DefenseValue: outcome.Target.Statistics.Defense,
                    RetaliationRolls: outcome.RetaliationRolls,
                    RetaliationSuccesses: outcome.RetaliationSuccesses,
                    Damage: outcome.Damage,
                    RetaliationDamage: outcome.RetaliationDamage,
                    DetectionRoll: outcome.DetectionRoll,
                    DetectionChance: outcome.DetectionChance,
                    RetaliationItemId: outcome.Target.WeaponItemId,
                    Attacker: outcome.Attacker.Details,
                    Defender: outcome.Target.Details),
                GameNotificationKind.Combat);
            results.Add(result);
            firstEventByGang.TryAdd(outcome.Target.Id, result.Event!.Sequence);
            firstEventByGang.TryAdd(outcome.Attacker.Id, result.Event.Sequence);
        }

        var policeResults = new List<PoliceAttackResolutionResult>(policeOutcomes.Length);
        foreach (var outcome in policeOutcomes)
        {
            var gang = state.FindGang(outcome.Target.Id)!;
            var details = new PoliceAttackResolutionDetails(
                outcome.Target.SectorId,
                outcome.DetectionChance,
                outcome.DetectionRoll,
                outcome.Detected,
                outcome.AttackValue,
                outcome.Target.Statistics.Defense,
                outcome.Rolls,
                outcome.Successes,
                Math.Min(outcome.Successes, outcome.Target.Force),
                outcome.Target.Force,
                gang.Force,
                outcome.Target.Details);
            var gameEvent = state.AppendPoliceAttackEvent(outcome.Target.Owner, outcome.Target.Id, details);
            state.QueueNotification(
                outcome.Target.Owner, GameNotificationKind.Police, outcome.Target.Id,
                outcome.Target.SectorId, gameEvent.Sequence);
            policeResults.Add(new PoliceAttackResolutionResult(
                outcome.Target.Id, outcome.Target.Owner, details, gameEvent));
            firstEventByGang.TryAdd(outcome.Target.Id, gameEvent.Sequence);
        }

        foreach (var snapshot in snapshots.Values
                     .Where(snapshot => snapshot.Force > 0 && state.FindGang(snapshot.Id)!.Force == 0)
                     .OrderBy(snapshot => snapshot.Id.Value))
        {
            var gang = state.FindGang(snapshot.Id)!;
            EliminateGang(state, gang);
            state.FindPlayer(gang.Owner)!.Statistics.Casualties++;
            state.QueueNotification(
                gang.Owner, GameNotificationKind.Elimination, gang.Id, gang.SectorId,
                firstEventByGang.GetValueOrDefault(gang.Id));
        }
        return new CombatPhaseResolution(results, policeResults);
    }

    private static PoliceCombatOutcome RollPoliceAttack(MatchState state, CombatSnapshot target)
    {
        var detectionChance = ManualRules.PoliceDetectionPercent(
            target.Statistics.Stealth, target.Hidden);
        var detectionRoll = state.Random.NextInclusive(100);
        var detected = detectionRoll <= detectionChance;
        var attackValue = detected ? ManualRules.PoliceAttackDiceCount(target.Statistics.Defense) : 0;
        var rolls = detected ? DiceRoller.RollD6(state.Random, attackValue) : [];
        return new PoliceCombatOutcome(
            target, detectionChance, detectionRoll, detected, attackValue,
            rolls, OriginalResolutionRules.CountSuccesses(
                rolls, ManualRules.PoliceSuccessThreshold));
    }

    private static void AddDamage(Dictionary<GangId, int> damage, GangId gang, int amount)
    {
        if (amount == 0) return;
        damage[gang] = checked(damage.GetValueOrDefault(gang) + amount);
    }

    private static void CreditCombatStatistics(
        MatchState state,
        IReadOnlyList<CombatOutcome> outcomes)
    {
        foreach (var outcome in outcomes.Where(outcome => outcome.Code == CommandResolutionCode.Resolved))
        {
            var statistics = state.FindPlayer(outcome.Attacker.Owner)!.Statistics;
            statistics.DamageInflicted = checked(statistics.DamageInflicted + outcome.Damage);
        }
    }

    /// <summary>
    /// Destroys a gang and takes its orders with it.
    /// </summary>
    /// <remarks>
    /// The queue entry has to go here, not at the end of the turn. A recurring command survives
    /// <see cref="TurnCommandQueue.FinishExecution"/>, so a gang killed in Combat would keep a Heal
    /// that restores its Force next turn, keep earning Chaos income and keep counting towards a
    /// sector capture — and the player cannot cancel it either, because cancellation refuses an
    /// eliminated gang. A retired queue entry is also what keeps a hire that reuses the dead gang's
    /// roster slot from leaving an order behind for a gang id that no longer resolves.
    /// The native resolver marks the record inactive without clearing its three equipment bytes.
    /// Keep those inaccessible values for parity and post-match inspection.
    /// </remarks>
    private static void EliminateGang(MatchState state, MatchGangState gang)
    {
        state.Commands.Cancel(gang.Id);
        gang.QueuedCommand = null;
        gang.Force = 0;
        gang.Hidden = false;
    }

    private sealed record CombatSnapshot(
        GangId Id,
        PlayerId Owner,
        int SectorId,
        int Force,
        bool Hidden,
        EffectiveStatistics Statistics,
        short? WeaponType,
        short? WeaponItemId,
        CombatantDetails Details)
    {
        public static CombatSnapshot For(MatchState state, MatchGangState gang) => new(
            gang.Id, gang.Owner, gang.SectorId, gang.Force, gang.Hidden,
            EffectiveStatisticsCalculator.ForGang(state, gang),
            gang.WeaponItemId is { } weapon ? state.Definitions.Items[weapon].Type : null,
            gang.WeaponItemId,
            CombatantDetails.Of(gang));
    }

    private sealed record CombatOutcome(
        QueuedCommand Queued,
        CombatSnapshot Attacker,
        CombatSnapshot Target,
        CommandResolutionCode Code,
        IReadOnlyList<int> AttackRolls,
        int AttackSuccesses,
        int Damage,
        IReadOnlyList<int> RetaliationRolls,
        int RetaliationSuccesses,
        int RetaliationDamage,
        int? DetectionRoll,
        int? DetectionChance);

    private sealed record PoliceCombatOutcome(
        CombatSnapshot Target,
        int DetectionChance,
        int DetectionRoll,
        bool Detected,
        int AttackValue,
        IReadOnlyList<int> Rolls,
        int Successes);

    private static CommandResolutionResult ResolveTerminate(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var before = gang.Force;
        EliminateGang(state, gang);
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before, 0),
            GameNotificationKind.Elimination);
    }

    /// <summary>
    /// Applies one normalised move.
    /// </summary>
    /// <remarks>
    /// There is no capacity test here. <see cref="NormalizeMoveDestinations"/> has already rewritten
    /// the player's whole move set so that no sector ends the phase with more than
    /// <see cref="MatchLimits.FriendlyGangsPerSector"/> friendly gangs, counting movers at their
    /// destination and everyone else where they stand. Re-testing per move measured the sector
    /// mid-phase instead, so a move into a sector a later-slotted gang was about to leave failed
    /// with <see cref="CommandResolutionCode.DestinationFull"/> purely because of roster order, and a
    /// move rewritten back to its own full sector failed where RULE-MOVE-001 and RULE-MOVE-002 ask for a successful
    /// no-op.
    /// </remarks>
    private static CommandResolutionResult ResolveMove(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var before = gang.SectorId;
        var destination = command.Target.Id;
        gang.SectorId = destination;
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before, destination),
            GameNotificationKind.Movement);
    }

    private static IReadOnlyList<CommandResolutionResult> ResolveMovementPhase(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        var ordered = commands
            .OrderBy(queued => queued.Command.Action == GangAction.Terminate ? 0 : 1)
            .ThenBy(queued => queued.Command.Player.Value)
            .ThenBy(queued => GangSlot(state, queued.Command))
            .ToArray();
        var results = new List<CommandResolutionResult>(ordered.Length);

        foreach (var queued in ordered.Where(queued => queued.Command.Action == GangAction.Terminate))
            results.Add(Resolve(state, queued));

        foreach (var playerMoves in ordered
                     .Where(queued => queued.Command.Action == GangAction.Move)
                     .GroupBy(queued => queued.Command.Player))
        {
            foreach (var queued in NormalizeMoveDestinations(state, playerMoves.ToArray()))
                results.Add(ResolveMove(state, queued.Command));
        }
        return results;
    }

    /// <summary>How many reroute draws one move set may spend before the deterministic fallback.</summary>
    private const int MaximumMoveRerouteDraws = 256;

    private static IReadOnlyList<QueuedCommand> NormalizeMoveDestinations(
        MatchState state,
        IReadOnlyList<QueuedCommand> moves)
    {
        if (moves.Count == 0) return [];
        var player = state.FindPlayer(moves[0].Command.Player)!;
        var normalized = moves.ToArray();
        var passes = 0;

        while (true)
        {
            var destinations = normalized.ToDictionary(
                queued => queued.Command.Gang,
                queued => queued.Command.Target.Id);
            var projectedCounts = new int[MatchLimits.SectorCount];
            foreach (var gang in player.Gangs.Where(gang => gang.IsActive))
                projectedCounts[destinations.GetValueOrDefault(gang.Id, gang.SectorId)]++;

            var overcrowdedSector = Enumerable.Range(0, MatchLimits.SectorCount)
                .LastOrDefault(
                    sectorId => projectedCounts[sectorId] > MatchLimits.FriendlyGangsPerSector,
                    -1);
            if (overcrowdedSector < 0) return normalized;

            var moveIndex = Array.FindIndex(normalized, queued =>
            {
                if (queued.Command.Target.Id != overcrowdedSector) return false;
                var gang = state.FindGang(queued.Command.Gang)!;
                return projectedCounts[gang.SectorId] < MatchLimits.FriendlyGangsPerSector;
            });
            if (moveIndex < 0)
                moveIndex = Array.FindIndex(normalized,
                    queued => queued.Command.Target.Id == overcrowdedSector);

            var selected = normalized[moveIndex];
            var selectedGang = state.FindGang(selected.Command.Gang)!;
            var replacement = selectedGang.SectorId;
            if (selected.Command.Target.Id == replacement && ++passes > MaximumMoveRerouteDraws)
            {
                // DEV-MOVE-002: a drawn neighbour can itself be crowded and send the same mover back
                // again, and some order sets repeat that for ever (FND-MOVE-006). Fall back to the
                // lowest-numbered sector with room. The board holds 64 x 6 gangs against a roster of
                // 80, so one always exists, and it cannot be the overcrowded sector.
                replacement = Array.FindIndex(
                    projectedCounts, count => count < MatchLimits.FriendlyGangsPerSector);
            }
            else if (selected.Command.Target.Id == replacement)
            {
                // RULE-MOVE-002, RULE-AI-007: a mover already sent back draws a random neighbour
                // with no capacity test; a later round repairs it if the neighbour overfills.
                replacement = OriginalAiSectorSelectionRules.RandomNeighbour(
                    selectedGang.SectorId, state.Random);
            }
            normalized[moveIndex] = selected with
            {
                Command = selected.Command with { Target = CommandTarget.Sector(replacement) }
            };
        }
    }

    /// <summary>
    /// RULE-CONTROL-001: each player's Control gangs pool their Force and Control per sector, and
    /// every sector with a Control order and no police is settled in ascending order.
    /// </summary>
    private static IReadOnlyList<CommandResolutionResult> ResolveControlPhase(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        var results = new List<CommandResolutionResult>(commands.Count);
        foreach (var sectorCommands in commands
            .GroupBy(queued => state.FindGang(queued.Command.Gang)!.SectorId)
            .OrderBy(group => group.Key))
        {
            var groups = sectorCommands
                .GroupBy(queued => queued.Command.Player)
                .OrderBy(group => group.Key.Value)
                .Select(group => group
                    .OrderBy(queued => GangSlot(state, queued.Command))
                    .ToArray())
                .ToArray();
            var sector = state.Sectors[sectorCommands.Key];
            // DEV-CONTROL-002: the original leaves a sector under police out of the pass without
            // a word; the rebuild records a failed result for each participant.
            results.AddRange(sector.CrackdownActive
                ? RefuseForCrackdown(state, sector, groups.SelectMany(group => group))
                : SettleControl(state, sector, groups));
        }
        return results;
    }

    private static IReadOnlyList<CommandResolutionResult> SettleControl(
        MatchState state,
        MatchSectorState sector,
        IReadOnlyList<QueuedCommand[]> groups)
    {
        var previousOwner = sector.Owner;
        // Income and the sector's Support, rebuilt before planning, are added to the owner's pool
        // and taken from every pool, so the owner's margin is its own Control plus its defense
        // [FND-CONTROL-003].
        var threshold = checked(sector.Income + sector.Support);
        var defense = previousOwner is { } owner ? OwnerDefense(state, sector, owner) : 0;
        var attacks = groups.ToDictionary(
            group => group[0].Command.Player,
            group => ManualRules.ControlStrength(ControlAttackers(state, group)));

        var best = 0;
        var candidates = new List<PlayerId?> { null };
        foreach (var player in state.Players.OrderBy(value => value.Id.Value))
        {
            var ordered = attacks.TryGetValue(player.Id, out var attack);
            // BUG-CONTROL-001: the original compares every player, so one with no order has the
            // margin -(Income + Support). DEV-CONTROL-001 leaves out such a player unless it owns
            // the sector.
            if (!ordered && player.Id != previousOwner) continue;
            var margin = player.Id == previousOwner
                ? checked(attack + defense)
                : checked(attack - threshold);
            if (margin == best) candidates.Add(player.Id);
            if (margin > best)
            {
                best = margin;
                candidates = [player.Id];
            }
        }
        var winner = candidates[0];
        int? chanceRoll = null;
        if (candidates.Count > 1)
        {
            chanceRoll = state.Random.NextInclusive(candidates.Count);
            winner = candidates[chanceRoll.Value - 1];
        }
        var captured = winner is not null && winner != previousOwner;
        if (captured)
        {
            if (previousOwner is { } oldOwner)
            {
                state.FindPlayer(winner!.Value)!.Statistics.Overthrows++;
                state.AiStrategy.RecordControl(oldOwner, winner.Value);
                SectorControlResolver.ResetInfluencedSites(state, sector, oldOwner);
            }
            else
            {
                SectorControlResolver.ResetSiteProgress(state, sector);
            }
            sector.Owner = winner;
        }

        var results = new List<CommandResolutionResult>(groups.Sum(group => group.Length));
        foreach (var group in groups)
        {
            var player = group[0].Command.Player;
            var drawn = chanceRoll is not null && candidates.Contains(player);
            foreach (var participant in group)
                results.Add(Complete(state, participant.Command, GameEventKind.CommandResolved,
                    new CommandResolutionDetails(
                        CommandResolutionCode.Resolved, [], captured && winner == player ? 1 : 0,
                        PreviousValue: previousOwner?.Value, ResultValue: sector.Owner?.Value,
                        AttackValue: attacks[player],
                        DefenseValue: checked(threshold + (player == previousOwner ? 0 : defense)),
                        ChanceRoll: drawn ? chanceRoll : null,
                        ChanceSides: drawn ? candidates.Count : null),
                    GameNotificationKind.Control));
        }
        // RULE-EVENT-013: the previous owner is told it lost the sector.
        if (captured && previousOwner is { } loser)
            state.QueueNotification(loser, GameNotificationKind.ControlLost,
                sectorId: sector.Id, executionPhase: ExecutionPhase.Control);
        return results;
    }

    /// <summary>The commands sorted by player and then by the gang's roster slot.</summary>
    private static QueuedCommand[] InRosterOrder(
        MatchState state,
        IEnumerable<QueuedCommand> commands) =>
        commands
            .OrderBy(queued => queued.Command.Player.Value)
            .ThenBy(queued => GangSlot(state, queued.Command))
            .ToArray();

    /// <summary>Fails every Control participant in a sector the police hold.</summary>
    private static CommandResolutionResult[] RefuseForCrackdown(
        MatchState state,
        MatchSectorState sector,
        IEnumerable<QueuedCommand> participants) =>
        participants.Select(participant => Complete(
            state, participant.Command, GameEventKind.CommandFailed,
            new CommandResolutionDetails(
                CommandResolutionCode.SectorInCrackdown, [], 0,
                PreviousValue: sector.Owner?.Value, ResultValue: sector.Owner?.Value),
            GameNotificationKind.Control)).ToArray();

    /// <summary>
    /// What the owner adds to its own Control pool besides Income and Support: the Force and
    /// Control of each of its gangs in the sector that is not hiding (RULE-CONTROL-001).
    /// </summary>
    private static int OwnerDefense(
        MatchState state,
        MatchSectorState sector,
        PlayerId owner) =>
        ManualRules.ControlStrength(state.FindPlayer(owner)!.Gangs
            .Where(gang => gang.IsActive && !gang.Hidden && gang.SectorId == sector.Id)
            .Select(gang => (gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Control)));

    private static IEnumerable<(int Force, int Control)> ControlAttackers(
        MatchState state,
        IEnumerable<QueuedCommand> participants) =>
        participants.Select(queued =>
        {
            var gang = state.FindGang(queued.Command.Gang)!;
            return (gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Control);
        });

    /// <summary>
    /// Rolls <paramref name="dice"/> six-sided dice and counts the ones that meet the band's
    /// threshold for <paramref name="action"/>. The dice are drawn before the threshold is looked up.
    /// </summary>
    private static (IReadOnlyList<int> Rolls, int Successes) RollAction(
        MatchState state,
        OriginalResolutionBand band,
        GangAction action,
        int dice)
    {
        var rolls = DiceRoller.RollD6(state.Random, dice);
        return (rolls, OriginalResolutionRules.CountSuccesses(
            rolls, OriginalResolutionRules.SuccessThreshold(band, action)));
    }

    private static CommandResolutionResult ResolveHeal(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        var band = OriginalResolutionRules.Band(state, command.Player);
        var (rolls, successes) = RollAction(
            state, band, GangAction.Heal, ManualRules.HealDiceCount(statistics.Heal));
        var before = gang.Force;
        gang.Force = ManualRules.RestoreForce(gang.Force, successes);
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, rolls, successes, before, gang.Force));
    }

    private static CommandResolutionResult ResolveHide(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var before = gang.Hidden;
        gang.Hidden = true;
        state.FindPlayer(gang.Owner)!.Statistics.TimesHidden++;
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before ? 1 : 0, 1));
    }

    private static CommandResolutionResult ResolveInfluence(MatchState state, GameCommand command)
    {
        var site = state.FindSite(command.Target.Id)!;
        var before = site.Resistance;
        if (before == 0)
            return Complete(state, command, GameEventKind.CommandResolved,
                new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, 0, 0),
                GameNotificationKind.Influence);

        var gang = state.FindGang(command.Gang)!;
        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        var band = OriginalResolutionRules.Band(state, command.Player);
        var pool = OriginalResolutionRules.ActionPool(
            band, GangAction.Influence,
            ManualRules.InfluenceDiceCount([(gang.Force, statistics.Influence)]));
        var (rolls, successes) = RollAction(state, band, GangAction.Influence, pool);
        site.Resistance = ManualRules.ApplyInfluenceProgress(before, successes);

        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(
                CommandResolutionCode.Resolved, rolls, successes, before, site.Resistance),
            GameNotificationKind.Influence);
    }

    private static CommandResolutionResult ResolveSnitch(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var sector = state.Sectors[gang.SectorId];
        var before = sector.BaseTolerance;
        var after = ToleranceResolver.ApplySnitch(state, sector);
        sector.BaseTolerance = after;
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before, after));
    }

    private static CommandResolutionResult ResolveResearch(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var player = state.FindPlayer(command.Player)!;
        var itemIndex = checked((short)command.Target.Id);
        var before = player.RemainingResearch(state.Definitions, itemIndex);
        if (before == 0)
        {
            return Complete(state, command, GameEventKind.CommandResolved,
                new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, 0, 0),
                GameNotificationKind.Research);
        }

        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        var band = OriginalResolutionRules.Band(state, command.Player);
        var pool = OriginalResolutionRules.ActionPool(
            band, GangAction.Research,
            ManualRules.ResearchDiceCount(gang.Force, statistics.Research));
        var (rolls, successes) = RollAction(state, band, GangAction.Research, pool);
        var after = player.ApplyResearch(state.Definitions, itemIndex, successes);
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, rolls, successes, before, after),
            GameNotificationKind.Research);
    }

    private static CommandResolutionResult Complete(
        MatchState state,
        GameCommand command,
        GameEventKind eventKind,
        CommandResolutionDetails resolution,
        GameNotificationKind notificationKind = GameNotificationKind.CommandResult,
        ExecutionPhase? eventExecutionPhase = null)
    {
        var gameEvent = state.AppendResolutionEvent(
            eventKind, command, resolution, eventExecutionPhase);
        state.QueueNotification(
            command.Player,
            notificationKind,
            command.Gang,
            state.FindGang(command.Gang)!.SectorId,
            gameEvent.Sequence,
            eventExecutionPhase);
        return new CommandResolutionResult(command, resolution.Code, gameEvent);
    }
}
