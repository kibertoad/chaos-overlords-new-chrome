namespace Rechaos.Core.GameModel;

public enum CommandResolutionCode : byte
{
    Resolved,
    InsufficientCash,
    UnsupportedAction,
    ItemUnavailable,
    DestinationFull,
    TargetHidden,
    TargetEvaded
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
public static class CommandResolver
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
        if (phase == ExecutionPhase.Combat) return ResolveCombatPhase(state, commands).Commands;
        if (phase == ExecutionPhase.Chaos) return ResolveChaosPhase(state, commands);
        var results = new List<CommandResolutionResult>(commands.Count);
        var resolvedSequences = new HashSet<long>();
        foreach (var queued in commands)
        {
            if (!resolvedSequences.Add(queued.Sequence)) continue;
            if (queued.Command.Action is not (GangAction.Influence or GangAction.Control))
            {
                results.Add(Resolve(state, queued));
                continue;
            }

            var sectorId = state.FindGang(queued.Command.Gang)!.SectorId;
            var participants = commands.Where(candidate =>
                candidate.Command.Action == queued.Command.Action
                && candidate.Command.Player == queued.Command.Player
                && (queued.Command.Action == GangAction.Influence
                    ? candidate.Command.Target == queued.Command.Target
                    : state.FindGang(candidate.Command.Gang)!.SectorId == sectorId)).ToArray();
            foreach (var participant in participants) resolvedSequences.Add(participant.Sequence);
            results.AddRange(queued.Command.Action == GangAction.Influence
                ? ResolveInfluence(state, participants)
                : ResolveControl(state, participants));
        }
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
            GangAction.Control => ResolveControl(state, [queued]).Single(),
            GangAction.Equip => ResolveEquip(state, queued.Command),
            GangAction.Give => ResolveGive(state, queued.Command),
            GangAction.Heal => ResolveHeal(state, queued.Command),
            GangAction.Hide => ResolveHide(state, queued.Command),
            GangAction.Influence => ResolveInfluence(state, [queued]).Single(),
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
            var tolerance = state.Sectors[state.FindGang(command.Gang)!.SectorId].Tolerance;
            return Complete(state, command, GameEventKind.CommandFailed,
                new CommandResolutionDetails(CommandResolutionCode.InsufficientCash, [], 0, tolerance, tolerance));
        }

        var gang = state.FindGang(command.Gang)!;
        player.Cash -= cost;
        player.Statistics.CashSpent += cost;
        var before = state.Sectors[gang.SectorId].Tolerance;
        var after = ManualRules.ApplyBribe(before);
        state.Sectors[gang.SectorId].Tolerance = after;
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

        var snapshots = state.Players.SelectMany(player => player.Gangs)
            .ToDictionary(gang => gang.Id, gang => CombatSnapshot.For(state, gang));
        var outcomes = new List<CombatOutcome>(commands.Count);
        foreach (var queued in commands)
        {
            var attacker = snapshots[queued.Command.Gang];
            var targetId = new GangId(queued.Command.Target.Id);
            var target = snapshots[targetId];
            int? detectionRoll = null;
            int? detectionChance = null;
            if (target.Hidden)
            {
                detectionChance = ManualRules.HiddenAttackHitPercent(
                    attacker.Statistics.Detect, target.Statistics.Stealth);
                detectionRoll = state.Random.NextInclusive(100);
                if (detectionRoll > detectionChance)
                {
                    outcomes.Add(new CombatOutcome(queued, attacker, target, CommandResolutionCode.TargetEvaded,
                        [], 0, 0, [], 0, 0, detectionRoll, detectionChance));
                    continue;
                }
            }

            var attackDice = ManualRules.AttackDiceCount(
                attacker.Force,
                ManualRules.CombatRating(attacker.Statistics, attacker.WeaponType),
                target.Statistics.Defense);
            var attackRolls = DiceRoller.RollD6(state.Random, attackDice);
            var attackSuccesses = ManualRules.CountSuccesses(attackRolls);
            var suppressesRetaliation = target.Hidden
                || ManualRules.SuppressesRetaliation(attacker.Statistics, attacker.WeaponType)
                && !ManualRules.SuppressesRetaliation(target.Statistics, target.WeaponType);
            var retaliationDice = suppressesRetaliation
                ? 0
                : ManualRules.AttackDiceCount(
                    target.Force,
                    ManualRules.CombatRating(target.Statistics, target.WeaponType),
                    attacker.Statistics.Defense);
            var retaliationRolls = DiceRoller.RollD6(state.Random, retaliationDice);
            var retaliationSuccesses = ManualRules.CountSuccesses(retaliationRolls);
            outcomes.Add(new CombatOutcome(
                queued, attacker, target, CommandResolutionCode.Resolved,
                attackRolls, attackSuccesses, attackSuccesses,
                retaliationRolls, retaliationSuccesses,
                ManualRules.RetaliationDamage(retaliationSuccesses),
                detectionRoll, detectionChance));
        }

        var policeOutcomes = snapshots.Values
            .Where(snapshot => snapshot.Force > 0 && state.Sectors[snapshot.SectorId].CrackdownActive)
            .OrderBy(snapshot => snapshot.SectorId)
            .ThenBy(snapshot => snapshot.Id.Value)
            .Select(snapshot => RollPoliceAttack(state, snapshot))
            .ToArray();

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
        CreditCombatStatistics(state, snapshots, outcomes);

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
                    AttackValue: outcome.AttackRolls.Count,
                    DefenseValue: outcome.Target.Statistics.Defense,
                    RetaliationRolls: outcome.RetaliationRolls,
                    RetaliationSuccesses: outcome.RetaliationSuccesses,
                    Damage: outcome.Damage,
                    RetaliationDamage: outcome.RetaliationDamage,
                    DetectionRoll: outcome.DetectionRoll,
                    DetectionChance: outcome.DetectionChance),
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
                gang.Force);
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
            EliminateGang(gang);
            state.FindPlayer(gang.Owner)!.Statistics.Casualties++;
            state.QueueNotification(
                gang.Owner, GameNotificationKind.Elimination, gang.Id, gang.SectorId,
                firstEventByGang.GetValueOrDefault(gang.Id));
        }
        return new CombatPhaseResolution(results, policeResults);
    }

    private static PoliceCombatOutcome RollPoliceAttack(MatchState state, CombatSnapshot target)
    {
        var detectionChance = ManualRules.PoliceDetectionPercent(target.Statistics.Stealth);
        var detectionRoll = state.Random.NextInclusive(100);
        var detected = detectionRoll <= detectionChance;
        var attackValue = detected ? Math.Max(0, ManualRules.PoliceCombat - target.Statistics.Defense) : 0;
        var rolls = detected ? DiceRoller.RollD6(state.Random, attackValue) : [];
        return new PoliceCombatOutcome(
            target, detectionChance, detectionRoll, detected, attackValue,
            rolls, ManualRules.CountSuccesses(rolls));
    }

    private static void AddDamage(Dictionary<GangId, int> damage, GangId gang, int amount)
    {
        if (amount == 0) return;
        damage[gang] = checked(damage.GetValueOrDefault(gang) + amount);
    }

    private static void CreditCombatStatistics(
        MatchState state,
        IReadOnlyDictionary<GangId, CombatSnapshot> snapshots,
        IReadOnlyList<CombatOutcome> outcomes)
    {
        var remainingForce = snapshots.ToDictionary(pair => pair.Key, pair => pair.Value.Force);
        foreach (var outcome in outcomes.Where(outcome => outcome.Code == CommandResolutionCode.Resolved))
        {
            Credit(outcome.Attacker.Owner, outcome.Target.Id, outcome.Damage);
            Credit(outcome.Target.Owner, outcome.Attacker.Id, outcome.RetaliationDamage);
        }

        void Credit(PlayerId source, GangId victim, int attempted)
        {
            var applied = Math.Min(attempted, remainingForce[victim]);
            remainingForce[victim] -= applied;
            state.FindPlayer(source)!.Statistics.DamageInflicted = checked(
                state.FindPlayer(source)!.Statistics.DamageInflicted + applied);
        }
    }

    private static void EliminateGang(MatchGangState gang)
    {
        gang.Force = 0;
        gang.Hidden = false;
        gang.WeaponItemId = null;
        gang.ArmorItemId = null;
        gang.MiscellaneousItemId = null;
    }

    private sealed record CombatSnapshot(
        GangId Id,
        PlayerId Owner,
        int SectorId,
        int Force,
        bool Hidden,
        EffectiveStatistics Statistics,
        short? WeaponType)
    {
        public static CombatSnapshot For(MatchState state, MatchGangState gang) => new(
            gang.Id, gang.Owner, gang.SectorId, gang.Force, gang.Hidden,
            EffectiveStatisticsCalculator.ForGang(state, gang),
            gang.WeaponItemId is { } weapon ? state.Definitions.Items[weapon].Type : null);
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

    private static CommandResolutionResult ResolveEquip(MatchState state, GameCommand command)
    {
        var player = state.FindPlayer(command.Player)!;
        var gang = state.FindGang(command.Gang)!;
        var itemIndex = checked((short)command.Target.Id);
        var item = state.Definitions.Items[itemIndex];
        if (player.Cash < item.Cost)
            return Complete(state, command, GameEventKind.CommandFailed,
                new CommandResolutionDetails(
                    CommandResolutionCode.InsufficientCash, [], 0, CashDelta: 0, ItemId: itemIndex),
                GameNotificationKind.Equipment);

        player.Cash -= item.Cost;
        player.Statistics.CashSpent += item.Cost;
        var replaced = EquipmentRules.Equip(gang, EquipmentRules.SlotFor(item), itemIndex);
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0,
                PreviousValue: replaced, ResultValue: itemIndex, CashDelta: -item.Cost,
                ItemId: itemIndex, ReplacedItemId: replaced),
            GameNotificationKind.Equipment);
    }

    private static CommandResolutionResult ResolveGive(MatchState state, GameCommand command)
    {
        var source = state.FindGang(command.Gang)!;
        var target = state.FindGang(new GangId(command.Target.Id))!;
        var itemIndex = checked((short)command.SecondaryTarget!.Value.Id);
        var slot = EquipmentRules.SlotFor(state.Definitions.Items[itemIndex]);
        if (EquipmentRules.EquippedItem(source, slot) != itemIndex)
            return Complete(state, command, GameEventKind.CommandFailed,
                new CommandResolutionDetails(
                    CommandResolutionCode.ItemUnavailable, [], 0, ItemId: itemIndex),
                GameNotificationKind.Equipment);

        EquipmentRules.Unequip(source, slot);
        var replaced = EquipmentRules.Equip(target, slot, itemIndex);
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0,
                PreviousValue: itemIndex, ResultValue: itemIndex,
                ItemId: itemIndex, ReplacedItemId: replaced),
            GameNotificationKind.Equipment);
    }

    private static CommandResolutionResult ResolveSell(MatchState state, GameCommand command)
    {
        var player = state.FindPlayer(command.Player)!;
        var gang = state.FindGang(command.Gang)!;
        var itemIndex = checked((short)command.Target.Id);
        var item = state.Definitions.Items[itemIndex];
        var slot = EquipmentRules.SlotFor(item);
        if (EquipmentRules.EquippedItem(gang, slot) != itemIndex)
            return Complete(state, command, GameEventKind.CommandFailed,
                new CommandResolutionDetails(
                    CommandResolutionCode.ItemUnavailable, [], 0, ItemId: itemIndex),
                GameNotificationKind.Equipment);

        EquipmentRules.Unequip(gang, slot);
        var proceeds = EquipmentRules.SaleValue(item);
        player.Cash = checked(player.Cash + proceeds);
        player.Statistics.CashEarned += proceeds;
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 0,
                PreviousValue: itemIndex, CashDelta: proceeds, ItemId: itemIndex),
            GameNotificationKind.Equipment);
    }

    private static CommandResolutionResult ResolveTerminate(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var before = gang.Force;
        EliminateGang(gang);
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before, 0),
            GameNotificationKind.Elimination);
    }

    private static CommandResolutionResult ResolveMove(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var destination = command.Target.Id;
        var friendlyCount = state.FindPlayer(command.Player)!.Gangs.Count(candidate =>
            candidate.IsActive && candidate.SectorId == destination);
        if (friendlyCount >= MatchLimits.FriendlyGangsPerSector)
            return Complete(state, command, GameEventKind.CommandFailed,
                new CommandResolutionDetails(
                    CommandResolutionCode.DestinationFull, [], 0, gang.SectorId, gang.SectorId),
                GameNotificationKind.Movement);

        var before = gang.SectorId;
        gang.SectorId = destination;
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before, destination),
            GameNotificationKind.Movement);
    }

    private static IReadOnlyList<CommandResolutionResult> ResolveChaosPhase(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        var groups = commands
            .GroupBy(queued => (
                queued.Command.Player,
                SectorId: state.FindGang(queued.Command.Gang)!.SectorId))
            .Select(group =>
            {
                var participants = group.ToArray();
                var sector = state.Sectors[group.Key.SectorId];
                var dice = ManualRules.ChaosDiceCount(
                    participants.Select(queued =>
                    {
                        var gang = state.FindGang(queued.Command.Gang)!;
                        return (gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Chaos);
                    }),
                    SectorIncome(state, sector));
                var rolls = DiceRoller.RollD6(state.Random, dice);
                return new ChaosGroup(participants, sector, rolls, ManualRules.CountSuccesses(rolls), dice);
            })
            .ToArray();

        var sectorSuccesses = groups
            .GroupBy(group => group.Sector.Id)
            .ToDictionary(group => group.Key, group => group.Sum(value => value.Successes));
        var sectorBefore = sectorSuccesses.Keys.ToDictionary(id => id, id => state.Sectors[id].Chaos);
        var newlyTriggered = new HashSet<int>();
        foreach (var (sectorId, successes) in sectorSuccesses.OrderBy(value => value.Key))
        {
            var sector = state.Sectors[sectorId];
            sector.Chaos = checked(sector.Chaos + successes);
            if (!sector.CrackdownActive && ManualRules.TriggersCrackdown(sector.Chaos, sector.Tolerance))
            {
                sector.CrackdownActive = true;
                newlyTriggered.Add(sectorId);
            }
        }

        var results = new List<CommandResolutionResult>(commands.Count);
        var firstEventBySector = new Dictionary<int, long>();
        foreach (var group in groups)
        {
            var player = state.FindPlayer(group.Participants[0].Command.Player)!;
            var payout = group.Sector.CrackdownActive
                ? 0
                : ManualRules.ChaosIncome(group.Successes, group.Sector.Owner == player.Id);
            player.Cash = checked(player.Cash + payout);
            player.Statistics.CashEarned += payout;
            for (var index = 0; index < group.Participants.Count; index++)
            {
                var participant = group.Participants[index];
                var result = Complete(state, participant.Command, GameEventKind.CommandResolved,
                    new CommandResolutionDetails(
                        CommandResolutionCode.Resolved, group.Rolls, group.Successes,
                        sectorBefore[group.Sector.Id], group.Sector.Chaos,
                        CashDelta: index == 0 ? payout : 0,
                        AttackValue: group.DiceCount, DefenseValue: group.Sector.Tolerance),
                    GameNotificationKind.Chaos);
                results.Add(result);
                firstEventBySector.TryAdd(group.Sector.Id, result.Event!.Sequence);
            }
        }

        foreach (var sectorId in newlyTriggered.Order())
        {
            foreach (var player in state.Players.Where(player => player.Status == PlayerStatus.Active))
                state.QueueNotification(
                    player.Id, GameNotificationKind.Crackdown,
                    sectorId: sectorId, relatedEventSequence: firstEventBySector[sectorId]);
        }
        return results;
    }

    private sealed record ChaosGroup(
        IReadOnlyList<QueuedCommand> Participants,
        MatchSectorState Sector,
        IReadOnlyList<int> Rolls,
        int Successes,
        int DiceCount);

    private static IReadOnlyList<CommandResolutionResult> ResolveControl(
        MatchState state,
        IReadOnlyList<QueuedCommand> participants)
    {
        if (participants.Count == 0) throw new ArgumentException("At least one participant is required.", nameof(participants));
        var first = participants[0].Command;
        var player = state.FindPlayer(first.Player)!;
        var sector = state.Sectors[state.FindGang(first.Gang)!.SectorId];
        var attackers = participants.Select(queued =>
        {
            var gang = state.FindGang(queued.Command.Gang)!;
            return (gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Control);
        });
        var attack = ManualRules.ControlStrength(attackers);
        var sectorIncome = SectorIncome(state, sector);
        var defense = 0;
        var support = 0;
        if (sector.Owner is { } owner && owner != first.Player)
        {
            defense = ManualRules.ControlStrength(state.FindPlayer(owner)!.Gangs
                .Where(gang => gang.IsActive && !gang.Hidden && gang.SectorId == sector.Id)
                .Select(gang => (gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Control)));
            support = sector.Sites.Where(site => site.InfluencedBy == owner).Sum(site =>
                state.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Support);
        }
        var margin = ManualRules.ControlMargin(attack, sectorIncome, defense, support);
        var previousOwner = sector.Owner;
        var captured = previousOwner != first.Player && margin > 0;
        if (captured)
        {
            if (previousOwner is { } oldOwner)
            {
                player.Statistics.Overthrows++;
                ResetInfluencedSites(state, sector, oldOwner);
            }
            sector.Owner = first.Player;
        }

        var results = new List<CommandResolutionResult>(participants.Count);
        foreach (var participant in participants)
        {
            results.Add(Complete(state, participant.Command, GameEventKind.CommandResolved,
                new CommandResolutionDetails(
                    CommandResolutionCode.Resolved, [], captured ? 1 : 0,
                    PreviousValue: previousOwner?.Value, ResultValue: sector.Owner?.Value,
                    AttackValue: attack, DefenseValue: checked(sectorIncome + defense + support)),
                GameNotificationKind.Control));
        }
        return results;
    }

    private static void ResetInfluencedSites(MatchState state, MatchSectorState sector, PlayerId previousOwner)
    {
        var player = state.FindPlayer(previousOwner)!;
        foreach (var site in sector.Sites)
        {
            var definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
            if (site.InfluencedBy == previousOwner) player.Support -= definition.Support;
            site.InfluencedBy = null;
            site.Resistance = definition.Resistance;
        }
    }

    private static int SectorIncome(MatchState state, MatchSectorState sector) =>
        sector.Sites.Sum(site =>
            state.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Cash);

    private static CommandResolutionResult ResolveHeal(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        var rolls = DiceRoller.RollD6(state.Random, ManualRules.HealDiceCount(statistics.Heal));
        var successes = ManualRules.CountSuccesses(rolls);
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
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before ? 1 : 0, 1));
    }

    private static IReadOnlyList<CommandResolutionResult> ResolveInfluence(
        MatchState state,
        IReadOnlyList<QueuedCommand> participants)
    {
        if (participants.Count == 0) throw new ArgumentException("At least one participant is required.", nameof(participants));
        var first = participants[0].Command;
        var site = state.FindSite(first.Target.Id)!;
        var dice = participants.Select(queued =>
        {
            var gang = state.FindGang(queued.Command.Gang)!;
            return (gang.Force, EffectiveStatisticsCalculator.ForGang(state, gang).Influence);
        });
        var rolls = DiceRoller.RollD6(state.Random, ManualRules.InfluenceDiceCount(dice));
        var successes = ManualRules.CountSuccesses(rolls);
        var before = site.Resistance;
        site.Resistance = ManualRules.ApplyInfluenceProgress(before, successes);
        if (site.Resistance == 0 && site.InfluencedBy is null)
        {
            site.InfluencedBy = first.Player;
            var definition = state.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
            state.FindPlayer(first.Player)!.Support = checked(state.FindPlayer(first.Player)!.Support + definition.Support);
        }

        var results = new List<CommandResolutionResult>(participants.Count);
        foreach (var participant in participants)
        {
            results.Add(Complete(state, participant.Command, GameEventKind.CommandResolved,
                new CommandResolutionDetails(
                    CommandResolutionCode.Resolved, rolls, successes, before, site.Resistance),
                GameNotificationKind.Influence));
        }
        return results;
    }

    private static CommandResolutionResult ResolveSnitch(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var before = state.Sectors[gang.SectorId].Tolerance;
        var after = ManualRules.ApplySnitch(before);
        state.Sectors[gang.SectorId].Tolerance = after;
        return Complete(state, command, GameEventKind.CommandResolved,
            new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0, before, after));
    }

    private static CommandResolutionResult ResolveResearch(MatchState state, GameCommand command)
    {
        var gang = state.FindGang(command.Gang)!;
        var player = state.FindPlayer(command.Player)!;
        var itemIndex = checked((short)command.Target.Id);
        var statistics = EffectiveStatisticsCalculator.ForGang(state, gang);
        var rolls = DiceRoller.RollD6(
            state.Random,
            ManualRules.ResearchDiceCount(gang.Force, statistics.Research));
        var successes = ManualRules.CountSuccesses(rolls);
        var before = player.RemainingResearch(state.Definitions, itemIndex);
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
        GameNotificationKind notificationKind = GameNotificationKind.CommandResult)
    {
        var gameEvent = state.AppendResolutionEvent(eventKind, command, resolution);
        state.QueueNotification(
            command.Player,
            notificationKind,
            command.Gang,
            state.FindGang(command.Gang)!.SectorId,
            gameEvent.Sequence);
        return new CommandResolutionResult(command, resolution.Code, gameEvent);
    }
}
