using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Every gang the interface has seen in the match, so a combat can still be drawn after the gang
/// that fought it has left the roster.
/// </summary>
/// <remarks>
/// <para>
/// A gang wiped out in Combat keeps its roster slot only until the Hire phase of the same turn: the
/// first hire its owner resolves reuses the slot, and the dead gang's id stops resolving through
/// <see cref="MatchState.FindGang"/>. The events of that turn still name it. Every combat reader
/// used to look both combatants up in the current state and quietly drop the fight when either was
/// missing, so the battles that eliminated a gang were exactly the ones Combat Summary, Combat
/// Detail and the automatic presentation never showed. Online every seat hires on most turns, which
/// made it the common case there.
/// </para>
/// <para>
/// The events carry only ids, and they are part of the state fingerprint, so recording who fought in
/// them would retire every stored match and online session. This is the presentation-side answer
/// instead: the interface observes the state on every frame, which always includes a frame in
/// Command before a turn resolves, and remembers each gang it sees. Ids are drawn from a counter
/// that never repeats, so a remembered gang can never be confused with a later one.
/// </para>
/// <para>
/// A gang absent from the current state is always a dead one — only an inactive slot is reused — so
/// it comes back with no force. What is lost is the turn a match was loaded on, or an online match
/// this client had not been playing was resumed on: the gangs retired while resolving it were
/// never on screen, and their fights stay unlisted.
/// </para>
/// </remarks>
public sealed class CombatantHistory
{
    private readonly Dictionary<GangId, Combatant> _seen = [];
    private readonly Dictionary<GangId, MatchGangState> _retired = [];
    private MatchState? _observedState;
    private ObservationKey _observedKey;
    private string? _onlineMatch;

    /// <summary>Remembers the gangs in <paramref name="state"/>.</summary>
    /// <remarks>
    /// Runs every frame, so it returns at once unless the state moved on: the roster only changes
    /// across a phase, a seat or an event, and each of those moves the key.
    /// </remarks>
    public void Observe(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var key = new ObservationKey(
            state.Coordinator.Turn,
            state.Coordinator.Phase,
            state.Coordinator.ExecutionPhase,
            state.Coordinator.ActivePlayer,
            state.Events.Count);
        if (ReferenceEquals(state, _observedState) && key == _observedKey) return;
        _observedState = state;
        _observedKey = key;
        foreach (var player in state.Players)
            foreach (var gang in player.Gangs)
                _seen[gang.Id] = Combatant.Of(gang);
    }

    /// <summary>
    /// The gang with <paramref name="id"/>: the live one when <paramref name="state"/> still holds
    /// it, otherwise the last sight of it with no force left.
    /// </summary>
    public MatchGangState? Find(MatchState state, GangId id)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.FindGang(id) is { } live) return live;
        if (_retired.TryGetValue(id, out var retired)) return retired;
        if (!_seen.TryGetValue(id, out var combatant)) return null;
        // Drawn every frame while a fight is on screen, so the stand-in is built once. A retired id
        // is never observed again, which is what keeps it from going stale.
        retired = combatant.Retired(id);
        _retired.Add(id, retired);
        return retired;
    }

    /// <summary>Forgets the match, for a new one or one loaded in its place.</summary>
    public void Clear()
    {
        _seen.Clear();
        _retired.Clear();
        _observedState = null;
        _observedKey = default;
        _onlineMatch = null;
    }

    /// <summary>
    /// Forgets the match unless <paramref name="onlineMatch"/> names the online match already
    /// remembered, which a resume of it carries on from.
    /// </summary>
    /// <remarks>
    /// A local match is always forgotten: loading a save can rewind the roster and hand its ids out
    /// again. An online match is never rewound, so the gangs its planning frames saw still answer
    /// for the turn that sealed while this client was reconnecting.
    /// </remarks>
    public void ResetTo(string? onlineMatch)
    {
        if (onlineMatch is null || !string.Equals(onlineMatch, _onlineMatch, StringComparison.Ordinal))
            Clear();
        _onlineMatch = onlineMatch;
    }

    private readonly record struct ObservationKey(
        int Turn,
        TurnPhase Phase,
        ExecutionPhase? ExecutionPhase,
        PlayerId? ActivePlayer,
        int Events);

    private readonly record struct Combatant(
        PlayerId Owner,
        short DefinitionId,
        int SectorId,
        short? WeaponItemId,
        short? ArmorItemId,
        short? MiscellaneousItemId)
    {
        public static Combatant Of(MatchGangState gang) => new(
            gang.Owner, gang.DefinitionId, gang.SectorId,
            gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId);

        public MatchGangState Retired(GangId id) => new(
            id, Owner, DefinitionId, SectorId, force: 0,
            WeaponItemId, ArmorItemId, MiscellaneousItemId);
    }
}

internal static class CombatantLookup
{
    /// <summary>
    /// A combatant named by an event: through <paramref name="history"/> when there is one, so a
    /// gang retired since the fight still resolves, otherwise only the live roster.
    /// </summary>
    public static MatchGangState? FindCombatant(
        this MatchState state,
        GangId id,
        CombatantHistory? history) =>
        history is null ? state.FindGang(id) : history.Find(state, id);
}
