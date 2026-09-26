using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The roster slot a combat presentation orders a player's gangs by (RULE-COMBAT-004): the slot
/// the events record for the gang when it fought, since a hire before the presentation can reuse
/// the slot of a gang the fight wiped out (FMT-STATE-008), else its current slot. A gang in
/// neither follows the gangs that hold one.
/// </summary>
public static class CombatRosterSlots
{
    /// <summary>The first slot the events record for each gang that fought, in event order.</summary>
    public static Dictionary<GangId, int> Recorded(IEnumerable<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var recorded = new Dictionary<GangId, int>();
        foreach (var gameEvent in events)
            foreach (var gang in Combatants(gameEvent))
                if (CombatantLookup.RecordedCombatant(gameEvent, gang)?.RosterSlot is { } slot)
                    recorded.TryAdd(gang, slot);
        return recorded;
    }

    /// <summary>
    /// The slot of each gang: the recorded one, else its index in <paramref name="roster"/>, else
    /// <see cref="int.MaxValue"/>. Each lookup is constant time.
    /// </summary>
    public static Func<GangId, int> Resolver(
        IReadOnlyDictionary<GangId, int> recorded,
        IReadOnlyList<MatchGangState> roster)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(roster);
        Dictionary<GangId, int>? current = null;
        return gang =>
        {
            if (recorded.TryGetValue(gang, out var slot)) return slot;
            current ??= roster.Select((member, index) => (member.Id, index))
                .ToDictionary(entry => entry.Id, entry => entry.index);
            return current.TryGetValue(gang, out slot) ? slot : int.MaxValue;
        };
    }

    // The gangs an event can record: the attacker or the police's target, and a gang defender.
    private static IEnumerable<GangId> Combatants(GameEvent gameEvent)
    {
        if (gameEvent.Gang is { } gang) yield return gang;
        if (gameEvent.PoliceAttack is null && gameEvent.Target.Kind == CommandTargetKind.Gang)
            yield return new GangId(gameEvent.Target.Id);
    }
}
