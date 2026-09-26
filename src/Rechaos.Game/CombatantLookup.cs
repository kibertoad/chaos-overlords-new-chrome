using System.Runtime.CompilerServices;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>Resolves the gangs a combat or police event names, including ones retired since.</summary>
/// <remarks>
/// A gang wiped out in Combat keeps its roster slot only until the Hire phase of the same turn: the
/// first hire its owner resolves reuses the slot, and the dead gang's id stops resolving through
/// <see cref="MatchState.FindGang"/>. Every combat reader used to look both combatants up in the
/// current state and quietly drop the fight when either was missing, so the battles that eliminated
/// a gang were exactly the ones Combat Summary, Combat Detail and the automatic presentation never
/// showed. The event records each combatant as it fought (<see cref="CombatantDetails"/>), so a
/// retired gang comes back from there, with no force left: only an inactive slot is ever reused.
/// </remarks>
internal static class CombatantLookup
{
    // Drawn every frame while a fight is on screen, so each retired stand-in is built once. Events
    // are immutable, and a record belongs to a single gang, so the stand-in can never go stale.
    private static readonly ConditionalWeakTable<CombatantDetails, MatchGangState> Retired = new();

    /// <summary>
    /// The gang <paramref name="id"/> that <paramref name="gameEvent"/> names: the live one while the
    /// roster still holds it, otherwise the one the event recorded.
    /// </summary>
    public static MatchGangState? FindCombatant(this MatchState state, GameEvent? gameEvent, GangId id)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.FindGang(id) is { } live) return live;
        if (gameEvent is null || Recorded(gameEvent, id) is not { } recorded) return null;
        if (!Retired.TryGetValue(recorded, out var retired))
        {
            retired = recorded.ToRetiredGang(state, id);
            Retired.AddOrUpdate(recorded, retired);
        }
        return retired;
    }

    private static CombatantDetails? Recorded(GameEvent gameEvent, GangId id)
    {
        if (gameEvent.PoliceAttack is { } police) return gameEvent.Gang == id ? police.Target : null;
        if (gameEvent.Resolution is not { } resolution) return null;
        if (gameEvent.Gang == id) return resolution.Attacker;
        return gameEvent.Target.Kind == CommandTargetKind.Gang && gameEvent.Target.Id == id.Value
            ? resolution.Defender
            : null;
    }
}
