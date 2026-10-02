using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private readonly Queue<PlayerId> _pendingFinalViews = [];
    private PlayerId? _finalViewPlayer;

    /// <summary>
    /// The player the planning screens are drawn for and act as: the one in its final view once
    /// the match has ended, otherwise the active player. The final view belongs to the live match,
    /// so a replay frame, which may come from a match with fewer seats, uses its own active player.
    /// </summary>
    private PlayerId? PlanningViewer =>
        (_replayPlayback is null ? _finalViewPlayer : null) ?? _state?.Coordinator.ActivePlayer;

    /// <summary>
    /// SCR-UI-003, FND-OBJECTIVE-004: a match the end evaluation finished gives every local human
    /// still seated a last visit before the awards, in slot order. A human the last turn
    /// eliminated gets the elimination card there instead, and one whose card was shown earlier
    /// gets nothing.
    /// </summary>
    private void BeginFinalViews()
    {
        _pendingFinalViews.Clear();
        if (_state is not null)
        {
            foreach (var playerId in FinalViewPresentation.Seats(_state, _presentedHotSeatEliminations))
                _pendingFinalViews.Enqueue(playerId);
        }
        ShowNextFinalView();
    }

    /// <summary>
    /// FND-OBJECTIVE-004: the next seat's Ready card, under the same test as during the match, then
    /// its elimination card or its final view; the awards after the last seat.
    /// </summary>
    private void ShowNextFinalView()
    {
        _finalViewPlayer = null;
        _gangSelection.Clear();
        ForgetGangDrag();
        ForgetHireDrag();
        if (_state is null || !_pendingFinalViews.TryDequeue(out var playerId))
        {
            _screens.Show(ClientScreen.Endgame);
            return;
        }

        _finalViewPlayer = playerId;
        _selectedGangIndex = 0;
        _message = string.Empty;
        if (_state.FindPlayer(playerId)?.Status != PlayerStatus.Active)
        {
            _eliminationHandoffPlayer = playerId;
            _presentedHotSeatEliminations.Add(playerId);
        }
        if (HotSeatHandoffPresentation.RequiresPrivateHandoff(_state))
            _screens.Show(ClientScreen.Handoff);
        else
            FinishHandoff();
    }

    private void ClearFinalViews()
    {
        _pendingFinalViews.Clear();
        _finalViewPlayer = null;
    }
}

public static class FinalViewPresentation
{
    /// <summary>
    /// FND-OBJECTIVE-004: the local humans the end sequence visits, in slot order: every human
    /// still active, and every human eliminated whose elimination card has not been shown.
    /// </summary>
    public static IReadOnlyList<PlayerId> Seats(MatchState state, IReadOnlySet<PlayerId> presentedEliminations)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(presentedEliminations);
        return state.Players
            .Where(player => player.Setup.Controller == PlayerController.Human
                && (player.Status == PlayerStatus.Active || !presentedEliminations.Contains(player.Id)))
            .OrderBy(player => player.Id.Value)
            .Select(player => player.Id)
            .ToArray();
    }
}
