using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Client-side screening of a hire offer dropped on a sector. The rules defer the sector
/// capacity check to resolution (docs/original-internals/commands-and-economy.md BIN-HIRE-001),
/// so a drop onto a sector that already holds a full complement of friendly gangs would be
/// reserved as a hire that can only fail. The drop is refused up front instead, measured the
/// same way a Move into that sector is, except that a gang the player has already ordered to
/// Move away or Terminate is not counted: both resolve in the Execution phase, before hires, so
/// the room it leaves is there when the hire is placed.
/// </summary>
public static class HireDropPlacement
{
    /// <summary>The refusal a drop on <paramref name="sectorId"/> earns, or null when it may be queued.</summary>
    public static HireValidation? Rejection(MatchState state, PlayerId player, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var stayingGangs = state.FindPlayer(player)?.Gangs
            .Count(gang => gang.IsActive && gang.SectorId == sectorId && !LeavesThisTurn(gang)) ?? 0;
        return stayingGangs >= MatchLimits.FriendlyGangsPerSector
            ? HireValidation.Reject(HireValidationCode.SectorCapacityReached)
            : null;
    }

    private static bool LeavesThisTurn(MatchGangState gang) =>
        gang.QueuedCommand?.Command switch
        {
            { Action: GangAction.Terminate } => true,
            { Action: GangAction.Move } move => move.Target.Id != gang.SectorId,
            _ => false
        };
}
