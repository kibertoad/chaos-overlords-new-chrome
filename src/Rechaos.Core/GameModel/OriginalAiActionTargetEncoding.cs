namespace Rechaos.Core.GameModel;

/// <summary>
/// The two target bytes the original's <c>plan</c> stores beside a planned action (RULE-AI-004),
/// and their encoding of a command a computer player submits.
/// </summary>
internal static class OriginalAiActionTargetEncoding
{
    /// <summary>
    /// How many target bytes <c>plan</c> stores for the action: both for an Attack, the first for
    /// a Move, Equip, Influence or Research, and none for any other (FND-AI-074). The handlers
    /// plan no Bribe, Give or Sell (FND-AI-079), so those store none either.
    /// </summary>
    public static int TargetBytesWritten(GangAction action) => action switch
    {
        GangAction.Attack => 2,
        GangAction.Move or GangAction.Equip or GangAction.Influence or GangAction.Research => 1,
        _ => 0
    };

    /// <summary>
    /// The target bytes of a validated command in the form <c>plan</c> stores them: the target
    /// gang's player and roster slot for an Attack, the site slot for an Influence, the sector for
    /// a Move and the item for an Equip or Research. An action that stores no target byte encodes
    /// as <see cref="AiActionTarget.None"/>.
    /// </summary>
    public static AiActionTarget Encode(MatchState state, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        return command.Action switch
        {
            GangAction.Attack => AttackTarget(state, command),
            GangAction.Influence => new(
                checked((byte)(command.Target.Id % MatchLimits.SitesPerSector)), 0),
            GangAction.Move or GangAction.Equip or GangAction.Research =>
                new(checked((byte)command.Target.Id), 0),
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
}
