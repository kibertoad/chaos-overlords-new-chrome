using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The forces a detailed-combat clip shows before and after its hits land: its defender's, and its
/// attacker's (<c>null</c> for the police), which drops by the retaliation it takes in that clip.
/// </summary>
public readonly record struct CombatClipForces(
    int DefenderBefore,
    int DefenderAfter,
    int? AttackerBefore,
    int? AttackerAfter)
{
    public int DefenderDamage => DefenderBefore - DefenderAfter;
    public int AttackerDamage => (AttackerBefore ?? 0) - (AttackerAfter ?? 0);
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
/// left it. Hits land in the order the clips play. The original has no retaliation clip: an
/// attack's clip lands its damage on the defender and the retaliation on the attacker together.
/// It also starts every bar at the phase-start force and moves it only by the clips it plays, so a
/// presentation timeline leaves out the hits of fights the viewer is not shown
/// (RULE-COMBAT-004).
/// </remarks>
public sealed class CombatForceTimeline
{
    private readonly MatchState _state;
    private readonly IReadOnlyList<GameEvent> _events;
    private readonly List<Hit> _hits = [];
    private readonly Dictionary<long, int> _clipOrder = [];
    private readonly Dictionary<GangId, int> _phaseDamage = [];
    private readonly Dictionary<GangId, int> _initialForces = [];

    private CombatForceTimeline(
        MatchState state,
        IReadOnlyList<GameEvent> events,
        IReadOnlyList<GameEvent> presented)
    {
        _state = state;
        _events = events;
        foreach (var gameEvent in events)
            foreach (var hit in HitsOf(gameEvent, 0))
                _phaseDamage[hit.Gang] = _phaseDamage.GetValueOrDefault(hit.Gang) + hit.Damage;
        for (var order = 0; order < presented.Count; order++)
        {
            _clipOrder.Add(presented[order].Sequence, order);
            _hits.AddRange(HitsOf(presented[order], order));
        }
    }

    /// <summary>The combat phase <paramref name="gameEvent"/> belongs to, played in event order.</summary>
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
        var phase = CombatPhaseEvents(events, gameEvent);
        return new CombatForceTimeline(state, phase, phase);
    }

    /// <summary>
    /// The combat phase of <paramref name="presented"/>, played in that order and moved only by
    /// those events.
    /// </summary>
    public static CombatForceTimeline ForPresentation(
        MatchState state,
        IReadOnlyList<GameEvent> presented)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(presented);
        if (presented.Count == 0)
            throw new ArgumentException("A presentation needs at least one event.", nameof(presented));
        var phase = CombatPhaseEvents(state.Events, presented[0]);
        if (presented.Any(gameEvent => IndexOf(phase, gameEvent.Sequence) < 0))
            throw new ArgumentException("Every presented event must belong to one combat phase.", nameof(presented));
        return new CombatForceTimeline(state, phase, presented);
    }

    /// <summary>Whether <paramref name="gameEvent"/> belongs to this timeline's combat phase.</summary>
    public bool Covers(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        return IndexOf(_events, gameEvent.Sequence) >= 0;
    }

    /// <summary>The forces the clip of the event at <paramref name="sequence"/> shows.</summary>
    public CombatClipForces Forces(long sequence, GangId? attacker, GangId defender)
    {
        // An event the timeline does not play shows the forces after every clip it does.
        var order = _clipOrder.TryGetValue(sequence, out var played) ? played : int.MaxValue;
        var step = Step(order);
        var defenderBefore = ForceBefore(defender, step);
        var defenderAfter = ForceAfter(defender, defenderBefore, order, step);
        if (attacker is not { } dealer)
            return new CombatClipForces(defenderBefore, defenderAfter, null, null);
        var attackerBefore = ForceBefore(dealer, step);
        return new CombatClipForces(
            defenderBefore, defenderAfter,
            attackerBefore, ForceAfter(dealer, attackerBefore, order, step));
    }

    /// <summary>The cap on one gang's damage in a combat phase (RULE-COMBAT-002).</summary>
    public const int PhaseDamageCap = 10;

    /// <summary>
    /// The Force <paramref name="gang"/> entered the combat phase with, the <c>force_start</c> of
    /// its combat record (RULE-COMBAT-002).
    /// </summary>
    public int PhaseStartForce(GangId gang) => InitialForce(gang);

    /// <summary>
    /// The <c>force_final</c> of <paramref name="gang"/>'s combat record: its phase-start Force less
    /// the phase's damage capped at 10, not clamped at 0 (RULE-COMBAT-002).
    /// </summary>
    public int PhaseFinalForce(GangId gang) =>
        InitialForce(gang) - Math.Min(PhaseDamageCap, _phaseDamage.GetValueOrDefault(gang));

    /// <summary>The force <paramref name="gang"/> keeps once the clip's own hits have landed.</summary>
    private int ForceAfter(GangId gang, int force, int order, int step)
    {
        for (var index = step; index < _hits.Count && _hits[index].Order == order; index++)
            if (_hits[index].Gang == gang)
                force = Math.Max(0, force - _hits[index].Damage);
        return force;
    }

    private int ForceBefore(GangId gang, int step)
    {
        var force = InitialForce(gang);
        for (var index = 0; index < step; index++)
            if (_hits[index].Gang == gang)
                force = Math.Max(0, force - _hits[index].Damage);
        return force;
    }

    /// <summary>The number of hits that land before the clip played at <paramref name="order"/>.</summary>
    private int Step(int order)
    {
        var step = 0;
        while (step < _hits.Count && _hits[step].Order < order) step++;
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
        var current = _state.FindGang(gang)?.Force ?? 0;
        return Math.Min(ManualRules.MaximumForce, current + _phaseDamage.GetValueOrDefault(gang));
    }

    private static IEnumerable<Hit> HitsOf(GameEvent gameEvent, int order)
    {
        if (gameEvent.Kind == GameEventKind.PoliceAttackResolved
            && gameEvent.PoliceAttack is { Detected: true } police
            && gameEvent.Gang is { } policeTarget)
        {
            yield return new Hit(order, policeTarget, police.Damage);
            yield break;
        }
        if (gameEvent.Kind != GameEventKind.CommandResolved
            || !IsGangAttack(gameEvent)
            || gameEvent.Gang is not { } attacker
            || gameEvent.Resolution is not { Code: CommandResolutionCode.Resolved } resolution)
            yield break;
        yield return new Hit(order, TargetGang(gameEvent), resolution.Damage);
        yield return new Hit(order, attacker, resolution.RetaliationDamage);
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

    private readonly record struct Hit(int Order, GangId Gang, int Damage);
}
