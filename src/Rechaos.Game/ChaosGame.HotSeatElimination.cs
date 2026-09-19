using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void QueueHotSeatEliminations(PlanningAdvance advance)
    {
        if (_session is not null || _state is not { Outcome: null } state) return;
        foreach (var playerId in advance.CrossedEliminatedPlayers)
            if (state.FindPlayer(playerId)?.Setup.Controller == PlayerController.Human)
                _pendingHotSeatEliminations.Enqueue(playerId);
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
