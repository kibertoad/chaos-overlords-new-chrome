using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// SCR-ATTACK-001: what the Attack picker has chosen. <see cref="Opponent"/> is the chosen
/// opponent's player, and <see cref="Target"/> the index into the picker's command list of the
/// chosen target, or null while no target is chosen.
/// </summary>
public readonly record struct AttackPickerSelection(PlayerId? Opponent, int? Target)
{
    public static AttackPickerSelection None => new(null, null);

    /// <summary>FND-ATTACK-003: Confirm needs both an opponent and a target.</summary>
    public bool CanConfirm => Opponent is not null && Target is not null;
}

/// <summary>
/// SCR-ATTACK-001: the picker's opponent cells, the target cells of the chosen opponent, and the
/// selection it opens with. <c>options</c> is the command list <see cref="AttackTargetRoster"/>
/// ordered, one Attack per targetable gang (RULE-ATTACK-002).
/// </summary>
public static class AttackPicker
{
    /// <summary>
    /// FND-ATTACK-003: the other five player slots in ascending order, skipping the acting player,
    /// one per opponent cell.
    /// </summary>
    public static IReadOnlyList<PlayerId> Opponents(PlayerId actingPlayer) =>
        Enumerable.Range(0, MatchLimits.PlayerCount)
            .Where(slot => slot != actingPlayer.Value)
            .Select(slot => new PlayerId(slot))
            .ToArray();

    /// <summary>
    /// FND-ATTACK-003: an opponent's cell is enabled when the acting player sees one of its gangs
    /// in the acting gang's sector, which is when the roster lists one of its gangs.
    /// </summary>
    public static bool IsOpponentEnabled(
        MatchState state, IReadOnlyList<GameCommand> options, PlayerId opponent) =>
        options.Any(command => TargetOwner(state, command) == opponent);

    /// <summary>
    /// RULE-ATTACK-002, FND-ATTACK-003: the command indices shown in the six target cells for
    /// <paramref name="opponent"/>, in roster order.
    /// </summary>
    public static IReadOnlyList<int> TargetCells(
        MatchState state, IReadOnlyList<GameCommand> options, PlayerId? opponent) => opponent is null
        ? []
        : Enumerable.Range(0, options.Count)
            .Where(index => TargetOwner(state, options[index]) == opponent)
            .Take(AttackCommandLayout.VisibleTargets)
            .ToArray();

    /// <summary>
    /// SCR-ATTACK-001, FND-ATTACK-003: the picker opens on the first enabled opponent with no
    /// target chosen. When the gang's order is already Attack and its target's player is enabled,
    /// that player is chosen instead, and its current target too when the cells list it.
    /// </summary>
    public static AttackPickerSelection Initial(
        MatchState state, IReadOnlyList<GameCommand> options, MatchGangState actor)
    {
        var enabled = Opponents(actor.Owner)
            .Where(opponent => IsOpponentEnabled(state, options, opponent))
            .ToArray();
        if (enabled.Length == 0) return AttackPickerSelection.None;
        if (actor.QueuedCommand?.Command is { Action: GangAction.Attack } current
            && state.FindGang(new GangId(current.Target.Id)) is { } currentTarget
            && enabled.Contains(currentTarget.Owner))
        {
            var cells = TargetCells(state, options, currentTarget.Owner);
            int? target = cells.FirstOrDefault(
                index => options[index].Target.Id == current.Target.Id, -1) is var found and >= 0
                ? found
                : null;
            return new AttackPickerSelection(currentTarget.Owner, target);
        }
        return new AttackPickerSelection(enabled[0], null);
    }

    private static PlayerId? TargetOwner(MatchState state, GameCommand command) =>
        state.FindGang(new GangId(command.Target.Id))?.Owner;
}
