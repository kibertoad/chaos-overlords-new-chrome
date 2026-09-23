using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The forces a detailed-combat clip shows: its defender before and after the hit lands, and its
/// attacker while the hit is dealt (<c>null</c> for the police).
/// </summary>
public readonly record struct CombatClipForces(int DefenderBefore, int DefenderAfter, int? Attacker)
{
    public int DefenderDamage => DefenderBefore - DefenderAfter;
}

/// <summary>
/// Replays one turn's combat phase hit by hit, so each detailed-combat clip shows the forces its
/// gangs have after the clips played before it.
/// </summary>
/// <remarks>
/// The rules resolve every attack of the phase at once, from forces snapshotted before the first
/// roll, and apply the summed damage afterwards. The panel presents that phase one clip at a time,
/// so drawing each clip from the final force plus the clip's own damage would show two gangs that
/// attack the same target both taking it from full force: the second must start where the first
/// left it. Hits are applied in event order, attack before retaliation, which is the order the
/// clips are queued in.
/// </remarks>
public sealed class CombatForceTimeline
{
    private readonly MatchState _state;
    private readonly IReadOnlyList<GameEvent> _events;
    private readonly List<Hit> _hits = [];
    private readonly Dictionary<GangId, int> _initialForces = [];

    private CombatForceTimeline(MatchState state, IReadOnlyList<GameEvent> events)
    {
        _state = state;
        _events = events;
        foreach (var gameEvent in events) AddHits(gameEvent);
    }

    /// <summary>The combat phase <paramref name="gameEvent"/> belongs to.</summary>
    public static CombatForceTimeline For(MatchState state, GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        return For(state, state.Events, gameEvent);
    }

    /// <summary>The combat phase <paramref name="gameEvent"/> belongs to within <paramref name="events"/>.</summary>
    internal static CombatForceTimeline For(
        MatchState state,
        IReadOnlyList<GameEvent> events,
        GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(gameEvent);
        return new CombatForceTimeline(state, CombatPhaseEvents(events, gameEvent));
    }

    /// <summary>The forces a clip of the event at <paramref name="sequence"/> shows.</summary>
    /// <param name="retaliation">Whether the clip is the event's retaliation rather than its attack.</param>
    public CombatClipForces Forces(long sequence, bool retaliation, GangId? attacker, GangId defender)
    {
        var step = Step(sequence, retaliation);
        var before = ForceBefore(defender, step);
        var after = step < _hits.Count && _hits[step].Sequence == sequence
                && _hits[step].Retaliation == retaliation && _hits[step].Gang == defender
            ? Math.Max(0, before - _hits[step].Damage)
            : before;
        return new CombatClipForces(
            before, after, attacker is { } dealer ? ForceBefore(dealer, step) : null);
    }

    private int ForceBefore(GangId gang, int step)
    {
        var force = InitialForce(gang);
        for (var index = 0; index < step; index++)
            if (_hits[index].Gang == gang)
                force = Math.Max(0, force - _hits[index].Damage);
        return force;
    }

    /// <summary>The number of hits that land before the clip identified by the arguments.</summary>
    private int Step(long sequence, bool retaliation)
    {
        var step = 0;
        while (step < _hits.Count
               && (_hits[step].Sequence < sequence
                   || _hits[step].Sequence == sequence && retaliation && !_hits[step].Retaliation))
            step++;
        return step;
    }

    /// <summary>
    /// The force the gang entered the phase with: recorded on any event that targets it, otherwise
    /// recovered from its current force, which is exact unless the phase wiped it out.
    /// </summary>
    private int InitialForce(GangId gang)
    {
        if (_initialForces.TryGetValue(gang, out var cached)) return cached;
        var force = RecordedInitialForce(gang) ?? RecoveredInitialForce(gang);
        _initialForces.Add(gang, force);
        return force;
    }

    private int? RecordedInitialForce(GangId gang)
    {
        foreach (var gameEvent in _events)
        {
            if (gameEvent.PoliceAttack is { } police && gameEvent.Gang == gang)
                return police.PreviousForce;
            if (IsGangAttack(gameEvent) && TargetGang(gameEvent) == gang
                && gameEvent.Resolution?.PreviousValue is { } previous)
                return previous;
        }
        return null;
    }

    private int RecoveredInitialForce(GangId gang)
    {
        var total = 0;
        foreach (var hit in _hits)
            if (hit.Gang == gang) total += hit.Damage;
        var current = _state.FindGang(gang)?.Force ?? 0;
        return Math.Min(ManualRules.MaximumForce, current + total);
    }

    private void AddHits(GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved
            && gameEvent.PoliceAttack is { Detected: true } police
            && gameEvent.Gang is { } policeTarget)
        {
            _hits.Add(new Hit(gameEvent.Sequence, Retaliation: false, policeTarget, police.Damage));
            return;
        }
        if (gameEvent.Kind != GameEventKind.CommandResolved
            || !IsGangAttack(gameEvent)
            || gameEvent.Gang is not { } attacker
            || gameEvent.Resolution is not { Code: CommandResolutionCode.Resolved } resolution)
            return;
        _hits.Add(new Hit(gameEvent.Sequence, Retaliation: false, TargetGang(gameEvent), resolution.Damage));
        _hits.Add(new Hit(gameEvent.Sequence, Retaliation: true, attacker, resolution.RetaliationDamage));
    }

    /// <summary>
    /// The combat-phase events of <paramref name="gameEvent"/>'s turn, or the event alone when it is
    /// not in the log.
    /// </summary>
    private static IReadOnlyList<GameEvent> CombatPhaseEvents(
        IReadOnlyList<GameEvent> events,
        GameEvent gameEvent)
    {
        var index = IndexOf(events, gameEvent.Sequence);
        if (index < 0) return [gameEvent];
        var first = index;
        while (first > 0 && events[first - 1].Turn == gameEvent.Turn) first--;
        var phase = new List<GameEvent>();
        for (var cursor = first; cursor < events.Count && events[cursor].Turn == gameEvent.Turn; cursor++)
            if (events[cursor].ExecutionPhase == ExecutionPhase.Combat)
                phase.Add(events[cursor]);
        return phase;
    }

    /// <summary>Binary search: the log is append-only in ascending sequence order.</summary>
    private static int IndexOf(IReadOnlyList<GameEvent> events, long sequence)
    {
        var low = 0;
        var high = events.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var found = events[middle].Sequence;
            if (found == sequence) return middle;
            if (found < sequence) low = middle + 1;
            else high = middle - 1;
        }
        return -1;
    }

    private static bool IsGangAttack(GameEvent gameEvent) =>
        gameEvent.Action == GangAction.Attack && gameEvent.Target.Kind == CommandTargetKind.Gang;

    private static GangId TargetGang(GameEvent gameEvent) => new(gameEvent.Target.Id);

    private readonly record struct Hit(long Sequence, bool Retaliation, GangId Gang, int Damage);
}
