namespace Rechaos.Core.GameModel;

/// <summary>
/// Encodes validated recreation commands into the two command-dependent bytes
/// retained by each original AI planning-history generation.
/// </summary>
internal static class OriginalAiActionTargetEncoding
{
    public static AiActionTarget Encode(MatchState state, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        return command.Action switch
        {
            GangAction.Attack => AttackTarget(state, command),
            GangAction.Equip or GangAction.Research =>
                new(checked((byte)command.Target.Id), 0),
            GangAction.Give => new(
                EquipmentMask(state, command.GiveTargets()),
                GangSlot(state, command.Player, command.Target.Id)),
            GangAction.Influence => new(
                checked((byte)(command.Target.Id % MatchLimits.SitesPerSector)), 0),
            GangAction.Move => new(checked((byte)command.Target.Id), 0),
            GangAction.Sell => new(EquipmentMask(state, command.SellTargets()), 0),
            _ => AiActionTarget.None
        };
    }

    private static AiActionTarget AttackTarget(MatchState state, GameCommand command)
    {
        var target = state.FindGang(new GangId(command.Target.Id))
            ?? throw new InvalidOperationException("Validated attack target is missing.");
        return new AiActionTarget(
            checked((byte)target.Owner.Value),
            GangSlot(state, target.Owner, target.Id.Value));
    }

    private static byte GangSlot(MatchState state, PlayerId owner, int gangId)
    {
        var gangs = state.FindPlayer(owner)?.Gangs
            ?? throw new InvalidOperationException("Validated target owner is missing.");
        for (var slot = 0; slot < gangs.Count; slot++)
            if (gangs[slot].Id.Value == gangId) return checked((byte)slot);
        throw new InvalidOperationException("Validated target gang has no stable player-list slot.");
    }

    private static byte EquipmentMask(MatchState state, int itemId)
    {
        var slot = EquipmentRules.SlotFor(state.Definitions.Items[itemId]);
        return checked((byte)(1 << (int)slot));
    }

    private static byte EquipmentMask(MatchState state, IEnumerable<CommandTarget> targets) =>
        targets.Aggregate((byte)0,
            (mask, target) => checked((byte)(mask | EquipmentMask(state, target.Id))));
}
