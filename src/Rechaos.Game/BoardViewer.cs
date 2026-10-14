using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The player the city board is drawn for.
/// </summary>
/// <remarks>
/// A local match plans its computers in the frames after a human ends planning, one turn per frame
/// at most, while the board stays on screen. Drawing the active player then put the computer's hire
/// offers, cash and map in front of the human for those frames. The board keeps the view of the
/// local human who planned last instead: the nearest still-active human seat before the active one
/// in slot order, wrapping round. With no active human left it draws the active player.
/// </remarks>
public static class BoardViewer
{
    public static PlayerId? For(MatchState state, PlayerId? planningViewer, bool online)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (online || planningViewer is not { } viewer
            || state.FindPlayer(viewer)?.Setup.Controller != PlayerController.Computer)
            return planningViewer;
        var count = state.Players.Count;
        for (var step = 1; step < count; step++)
        {
            var candidate = state.Players[((viewer.Value - step) % count + count) % count];
            if (candidate.Setup.Controller == PlayerController.Human
                && candidate.Status == PlayerStatus.Active)
                return candidate.Id;
        }
        return planningViewer;
    }
}
