using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void QueueHotSeatEliminations(PlanningAdvance advance)
    {
        if (_session is not null || _state is not { Outcome: null } state) return;
        var humans = advance.CrossedEliminatedPlayers.Where(playerId =>
            state.FindPlayer(playerId)?.Setup.Controller == PlayerController.Human);
        foreach (var playerId in HotSeatEliminationPresentation.QueueUnpresented(
                     humans, _presentedHotSeatEliminations))
            _pendingHotSeatEliminations.Enqueue(playerId);
    }

    private void ResetHotSeatEliminationPresentation(bool acknowledgeExistingEliminations)
    {
        _pendingHotSeatEliminations.Clear();
        _presentedHotSeatEliminations.Clear();
        _eliminationHandoffPlayer = null;
        if (!acknowledgeExistingEliminations || _state is null) return;
        foreach (var player in _state.Players.Where(player =>
                     player.Setup.Controller == PlayerController.Human
                     && player.Status == PlayerStatus.Eliminated))
            _presentedHotSeatEliminations.Add(player.Id);
    }

    private bool ShowPendingHotSeatElimination()
    {
        if (!_pendingHotSeatEliminations.TryDequeue(out var playerId)) return false;
        _eliminationHandoffPlayer = playerId;
        _screens.Show(ClientScreen.Handoff);
        return true;
    }

    private void HandleHotSeatEliminationClick(Point point)
    {
        if (!EndgameNoticeLayout.Panel.Contains(point)) return;
        PlayGeneralSound(AudioRouting.PointerPushSound());
        FinishHotSeatEliminationPresentation();
    }

    private void FinishHotSeatEliminationPresentation()
    {
        _eliminationHandoffPlayer = null;
        if (ShowPendingHotSeatElimination()) return;

        if (_state?.Coordinator.ActivePlayer is { } playerId
            && _state.FindPlayer(playerId)?.Setup.Controller == PlayerController.Human)
        {
            _screens.Show(ClientScreen.Handoff);
            return;
        }

        _screens.Show(ClientScreen.City);
    }

    private void DrawHotSeatElimination(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        MatchState state)
    {
        if (_eliminationHandoffPlayer is not { } playerId
            || state.FindPlayer(playerId) is not { } player)
        {
            _screens.Show(ClientScreen.City);
            return;
        }

        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        if (_eliminationBackground is not null)
            batch.Draw(_eliminationBackground, EndgameNoticeLayout.Panel, Color.White);
        else
            batch.Draw(pixel, EndgameNoticeLayout.Panel, Color.Black);
        if (_uiSprites is not null)
            batch.Draw(_uiSprites, EndgameNoticeLayout.Portrait,
                OriginalSpriteLayout.OverlordPortrait(player.Setup.PortraitId), Color.White);
        DrawBorder(batch, pixel, EndgameNoticeLayout.Portrait, PlayerColors[playerId.Value], 1);
        font.Draw(batch, "PRESS ENTER OR CLICK TO CONTINUE", new Vector2(66, 438), Color.White, 1);
    }
}

/// <summary>
/// Keeps an eliminated local seat's private presentation one-shot while the coordinator continues
/// to cross its permanently retired command slot on later rounds.
/// </summary>
public static class HotSeatEliminationPresentation
{
    public static IReadOnlyList<PlayerId> QueueUnpresented(
        IEnumerable<PlayerId> crossedEliminatedPlayers,
        ISet<PlayerId> presentedPlayers)
    {
        ArgumentNullException.ThrowIfNull(crossedEliminatedPlayers);
        ArgumentNullException.ThrowIfNull(presentedPlayers);
        var queued = new List<PlayerId>();
        foreach (var playerId in crossedEliminatedPlayers)
            if (presentedPlayers.Add(playerId)) queued.Add(playerId);
        return queued;
    }
}
