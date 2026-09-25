using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>One event of a detailed-combat presentation, in the order it plays.</summary>
/// <param name="HandsOff">
/// Whether the event is a gang's attack whose target attacked it back, which plays next: its clip
/// cuts to that reply at the final-result tick instead of holding the result.
/// </param>
public readonly record struct PresentedCombatEvent(GameEvent Event, bool HandsOff);

/// <summary>
/// The order the original's Detailed Combat plays one combat phase for a viewer
/// (RULE-COMBAT-004).
/// </summary>
/// <remarks>
/// The original walks only the viewer's gangs that fought, by sector and then by the slot each
/// gang took in its sector's combat table. The resolver hands those slots out in roster order, so
/// the roster order within a sector is the slot order. For each gang it plays the gang's own
/// attack, then its target's attack on it when the two attacked each other, then every other
/// attack on it in player and roster order (the event order), then the police.
/// </remarks>
public static class CombatPresentationOrder
{
    /// <param name="phase">The events of one combat phase the viewer can see, in event order.</param>
    public static IReadOnlyList<PresentedCombatEvent> Order(
        MatchState state,
        IReadOnlyList<GameEvent> phase,
        PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(phase);

        var focalSectors = new Dictionary<GangId, int>();
        foreach (var gameEvent in phase)
            foreach (var (gang, owner, sector) in Combatants(state, gameEvent))
                if (owner == viewer) focalSectors.TryAdd(gang, sector);

        var roster = state.FindPlayer(viewer)?.Gangs ?? [];
        // A gang the fight wiped out can have lost its roster slot to a hire in the same turn; it
        // has no slot left to order by, so it follows the gangs that still hold theirs.
        int RosterSlot(GangId gang)
        {
            for (var index = 0; index < roster.Count; index++)
                if (roster[index].Id == gang) return index;
            return int.MaxValue;
        }

        var presented = new List<PresentedCombatEvent>(phase.Count);
        var emitted = new HashSet<long>();
        void Emit(GameEvent gameEvent, bool handsOff = false)
        {
            if (emitted.Add(gameEvent.Sequence)) presented.Add(new PresentedCombatEvent(gameEvent, handsOff));
        }

        foreach (var focal in focalSectors
                     .OrderBy(entry => entry.Value)
                     .ThenBy(entry => RosterSlot(entry.Key))
                     .ThenBy(entry => entry.Key.Value)
                     .Select(entry => entry.Key))
        {
            if (phase.FirstOrDefault(gameEvent => IsGangAttack(gameEvent) && gameEvent.Gang == focal)
                is { } own)
            {
                var reply = phase.FirstOrDefault(gameEvent => IsGangAttack(gameEvent)
                    && gameEvent.Gang == TargetGang(own)
                    && TargetGang(gameEvent) == focal
                    && !emitted.Contains(gameEvent.Sequence));
                Emit(own, handsOff: reply is not null && !emitted.Contains(own.Sequence));
                if (reply is not null) Emit(reply);
            }
            foreach (var gameEvent in phase)
                if (IsGangAttack(gameEvent) && TargetGang(gameEvent) == focal)
                    Emit(gameEvent);
            foreach (var gameEvent in phase)
                if (gameEvent.Kind == GameEventKind.PoliceAttackResolved && gameEvent.Gang == focal)
                    Emit(gameEvent);
        }
        // Anything no gang of the viewer's accounts for still plays, after the rest.
        foreach (var gameEvent in phase) Emit(gameEvent);
        return presented;
    }

    /// <summary>The gangs <paramref name="gameEvent"/> puts in the fight, with owner and sector.</summary>
    private static IEnumerable<(GangId Gang, PlayerId Owner, int Sector)> Combatants(
        MatchState state,
        GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved)
        {
            if (gameEvent.Gang is { } target && gameEvent.PoliceAttack is { } police)
                yield return (target, gameEvent.Player, police.SectorId);
            yield break;
        }
        if (!IsGangAttack(gameEvent) || gameEvent.Gang is not { } attacker) yield break;
        var resolution = gameEvent.Resolution;
        if (resolution?.Attacker is { } attackerDetails)
            yield return (attacker, attackerDetails.Owner, attackerDetails.SectorId);
        else if (state.FindCombatant(gameEvent, attacker) is { } attackingGang)
            yield return (attacker, attackingGang.Owner, attackingGang.SectorId);
        var defender = TargetGang(gameEvent);
        if (resolution?.Defender is { } defenderDetails)
            yield return (defender, defenderDetails.Owner, defenderDetails.SectorId);
        else if (state.FindCombatant(gameEvent, defender) is { } defendingGang)
            yield return (defender, defendingGang.Owner, defendingGang.SectorId);
    }

    private static bool IsGangAttack(GameEvent gameEvent) =>
        gameEvent.Action == GangAction.Attack && gameEvent.Target.Kind == CommandTargetKind.Gang;

    private static GangId TargetGang(GameEvent gameEvent) => new(gameEvent.Target.Id);
}
