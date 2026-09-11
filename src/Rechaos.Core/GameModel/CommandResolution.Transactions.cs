namespace Rechaos.Core.GameModel;

public static partial class CommandResolver
{
    private static CommandResolutionResult ResolveEquip(MatchState state, GameCommand command)
    {
        var details = ApplyEquip(state, command);
        return CompleteTransaction(state, command, details);
    }

    private static CommandResolutionDetails ApplyEquip(MatchState state, GameCommand command)
    {
        var player = state.FindPlayer(command.Player)!;
        var gang = state.FindGang(command.Gang)!;
        var itemIndex = checked((short)command.Target.Id);
        var item = state.Definitions.Items[itemIndex];
        var cost = SpecialSiteRules.EquipmentCost(state, gang, item);
        if (player.Cash < cost)
            return new CommandResolutionDetails(
                CommandResolutionCode.InsufficientCash, [], 0, CashDelta: 0, ItemId: itemIndex);

        player.Cash -= cost;
        player.Statistics.CashSpent += cost;
        var replaced = EquipmentRules.Equip(gang, EquipmentRules.SlotFor(item), itemIndex);
        return new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0,
            PreviousValue: replaced, ResultValue: itemIndex, CashDelta: -cost,
            ItemId: itemIndex, ReplacedItemId: replaced);
    }

    private static CommandResolutionResult ResolveGive(MatchState state, GameCommand command)
    {
        var give = PrepareGive(state, command);
        if (!give.Available)
            return CompleteTransaction(state, command, new CommandResolutionDetails(
                CommandResolutionCode.ItemUnavailable, [], 0, ItemId: give.UnavailableItem));

        foreach (var itemIndex in give.Items)
            EquipmentRules.Unequip(state.FindGang(command.Gang)!,
                EquipmentRules.SlotFor(state.Definitions.Items[itemIndex]));
        return CompleteTransaction(state, command, ApplyGive(state, command, give.Items));
    }

    private static IReadOnlyList<CommandResolutionResult> ResolveTransactionPhase(
        MatchState state,
        IReadOnlyList<QueuedCommand> commands)
    {
        var ordered = commands
            .OrderBy(queued => queued.Command.Player.Value)
            .ThenBy(queued => GangSlot(state, queued.Command))
            .ToArray();
        var prepared = ordered.Where(queued => queued.Command.Action == GangAction.Give)
            .ToDictionary(queued => queued.Sequence, queued => PrepareGive(state, queued.Command));
        foreach (var give in prepared.Values.Where(give => give.Available))
        foreach (var itemIndex in give.Items)
            EquipmentRules.Unequip(state.FindGang(give.Command.Gang)!,
                EquipmentRules.SlotFor(state.Definitions.Items[itemIndex]));

        var details = new Dictionary<long, CommandResolutionDetails>();
        foreach (var queued in ordered)
        {
            details[queued.Sequence] = queued.Command.Action switch
            {
                GangAction.Equip => ApplyEquip(state, queued.Command),
                GangAction.Sell => ApplySell(state, queued.Command),
                GangAction.Give when !prepared[queued.Sequence].Available =>
                    new CommandResolutionDetails(CommandResolutionCode.ItemUnavailable, [], 0,
                        ItemId: prepared[queued.Sequence].UnavailableItem),
                GangAction.Give => new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0),
                _ => throw new InvalidOperationException("Unsupported transaction action.")
            };
        }

        foreach (var queued in ordered.Where(queued => queued.Command.Action == GangAction.Give))
        {
            var give = prepared[queued.Sequence];
            if (give.Available) details[queued.Sequence] = ApplyGive(state, give.Command, give.Items);
        }

        return ordered.Select(queued =>
            CompleteTransaction(state, queued.Command, details[queued.Sequence])).ToArray();
    }

    private static CommandResolutionResult CompleteTransaction(
        MatchState state,
        GameCommand command,
        CommandResolutionDetails details) =>
        Complete(state, command,
            details.Code == CommandResolutionCode.Resolved
                ? GameEventKind.CommandResolved
                : GameEventKind.CommandFailed,
            details, GameNotificationKind.Equipment);

    private static int GangSlot(MatchState state, GameCommand command)
        => GangSlot(state, command.Player, command.Gang);

    private static int GangSlot(MatchState state, PlayerId player, GangId gangId)
    {
        var gangs = state.FindPlayer(player)!.Gangs;
        for (var index = 0; index < gangs.Count; index++)
            if (gangs[index].Id == gangId) return index;
        throw new InvalidOperationException(
            $"Gang {gangId} is not in player {player}'s roster.");
    }

    private static PreparedGive PrepareGive(MatchState state, GameCommand command)
    {
        var source = state.FindGang(command.Gang)!;
        var items = command.GiveTargets().Select(target => checked((short)target.Id)).ToArray();
        var unavailable = items.FirstOrDefault(itemIndex =>
            EquipmentRules.EquippedItem(source,
                EquipmentRules.SlotFor(state.Definitions.Items[itemIndex])) != itemIndex, (short)-1);
        return new PreparedGive(command, items, unavailable < 0, unavailable < 0 ? null : unavailable);
    }

    private static CommandResolutionDetails ApplyGive(
        MatchState state,
        GameCommand command,
        IReadOnlyList<short> items)
    {
        var target = state.FindGang(new GangId(command.Target.Id))!;
        var replaced = new List<short>();
        foreach (var itemIndex in items)
        {
            var slot = EquipmentRules.SlotFor(state.Definitions.Items[itemIndex]);
            if (EquipmentRules.Equip(target, slot, itemIndex) is { } replacedItem)
                replaced.Add(replacedItem);
        }
        return new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0,
            PreviousValue: items[0], ResultValue: items[0],
            ItemId: items[0], ReplacedItemId: replaced.Count > 0 ? replaced[0] : null,
            ItemIds: items, ReplacedItemIds: replaced);
    }

    private sealed record PreparedGive(
        GameCommand Command,
        IReadOnlyList<short> Items,
        bool Available,
        short? UnavailableItem);

    private static CommandResolutionResult ResolveSell(MatchState state, GameCommand command)
    {
        var details = ApplySell(state, command);
        return CompleteTransaction(state, command, details);
    }

    private static CommandResolutionDetails ApplySell(MatchState state, GameCommand command)
    {
        var player = state.FindPlayer(command.Player)!;
        var gang = state.FindGang(command.Gang)!;
        var selected = command.SellTargets()
            .Select(target => checked((short)target.Id))
            .Select(itemIndex => (ItemIndex: itemIndex, Item: state.Definitions.Items[itemIndex]))
            .ToArray();
        var unavailable = selected.FirstOrDefault(entry =>
            EquipmentRules.EquippedItem(gang, EquipmentRules.SlotFor(entry.Item)) != entry.ItemIndex);
        if (unavailable.Item is not null)
            return new CommandResolutionDetails(
                CommandResolutionCode.ItemUnavailable, [], 0, ItemId: unavailable.ItemIndex);

        foreach (var entry in selected)
            EquipmentRules.Unequip(gang, EquipmentRules.SlotFor(entry.Item));
        var credited = selected.OrderBy(entry => EquipmentRules.SlotFor(entry.Item)).Last();
        var proceeds = EquipmentRules.SaleValue(credited.Item);
        player.Cash = checked(player.Cash + proceeds);
        player.Statistics.CashEarned += proceeds;
        return new CommandResolutionDetails(
            CommandResolutionCode.Resolved, [], 0,
            PreviousValue: selected[0].ItemIndex, CashDelta: proceeds,
            ItemId: selected[0].ItemIndex,
            ItemIds: selected.Select(entry => entry.ItemIndex).ToArray());
    }
}
