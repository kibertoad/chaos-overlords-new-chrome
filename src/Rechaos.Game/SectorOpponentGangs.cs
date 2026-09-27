using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Whose gangs the Sector workspace lists. The viewer reads their own roster by default and may
/// borrow the cards for an opponent whose gangs the sector's detection rules already expose, so
/// the workspace never reveals a gang the viewer could not otherwise see.
/// </summary>
public static class SectorOpponentGangs
{
    /// <summary>
    /// RULE-UI-010 <c>sector_card_slots</c>: the overlord's active gangs in the sector that
    /// <paramref name="viewer"/> can see, in roster slot order.
    /// </summary>
    public static IReadOnlyList<MatchGangState> InSector(
        MatchState state,
        PlayerId viewer,
        PlayerId owner,
        int sectorId) =>
        Detected(state, viewer, owner, sectorId).ToArray();

    /// <summary>
    /// Whether the opponent keeps at least one gang in the sector that the viewer can see. The
    /// viewer's own identity is never an opponent, so their own portrait never advertises gangs.
    /// </summary>
    public static bool Detectable(
        MatchState state,
        PlayerId viewer,
        PlayerId owner,
        int sectorId) =>
        owner != viewer && Detected(state, viewer, owner, sectorId).Any();

    /// <summary>
    /// For every seat, whether <paramref name="viewer"/> sees at least one active gang of that
    /// seat's overlord in the sector: the portraits the sector view's Overlord bar leaves undimmed
    /// (FND-UI-018). One pass over the gangs, for a bar drawn every frame.
    /// </summary>
    public static bool[] SeenSeats(MatchState state, PlayerId viewer, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.FindPlayer(viewer) is null) throw new ArgumentOutOfRangeException(nameof(viewer));
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        var seen = new bool[MatchLimits.PlayerCount];
        foreach (var player in state.Players)
        {
            var owner = player.Id;
            foreach (var gang in player.Gangs)
            {
                if (!gang.IsActive || gang.SectorId != sectorId
                    || (owner != viewer && !state.CanPlayerDetectGang(viewer, gang.Id)))
                    continue;
                seen[owner.Value] = true;
                break;
            }
        }
        return seen;
    }

    /// <summary>
    /// The overlord whose portrait on the Overlord bar covers the point, when one does
    /// (FND-UI-015: <c>(12 + 70n, 5, 62, 32)</c>).
    /// </summary>
    public static PlayerId? PortraitAt(MatchState state, Point point)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var player in state.Setup.Players)
            if (OverlordBarLayout.PortraitHit(player.Id.Value).Contains(point))
                return player.Id;
        return null;
    }

    private static IEnumerable<MatchGangState> Detected(
        MatchState state,
        PlayerId viewer,
        PlayerId owner,
        int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.FindPlayer(viewer) is null) throw new ArgumentOutOfRangeException(nameof(viewer));
        var player = state.FindPlayer(owner) ?? throw new ArgumentOutOfRangeException(nameof(owner));
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));
        return player.Gangs.Where(gang => gang.IsActive && gang.SectorId == sectorId
            && (owner == viewer || state.CanPlayerDetectGang(viewer, gang.Id)));
    }
}
