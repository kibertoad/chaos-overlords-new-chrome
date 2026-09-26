using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>
    /// Spaces out redraws while the window has no focus; <see cref="Update"/> keeps its cadence.
    /// </summary>
    /// <remarks>
    /// Returning false skips both <see cref="Draw"/> and the present for this tick, the same path
    /// MonoGame takes when the graphics device is not ready, so the last presented frame stays on
    /// screen until the cadence allows the next one.
    /// </remarks>
    protected override bool BeginDraw() =>
        _backgroundRedrawCadence.ShouldRedraw(IsActive, _inputTime) && base.BeginDraw();

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(8, 10, 12));
        if (_batch is null || _pixel is null || _font is null) return;
        if (_introMoviesPlaying)
        {
            DrawIntroMovie(_batch);
            CompleteDraw(gameTime);
            return;
        }
        var viewport = GraphicsDevice.Viewport;
        if (_creditsOpen)
        {
            _batch.Begin(samplerState: SamplerState.PointClamp,
                transformMatrix: VirtualInput.Transform(viewport));
            DrawCredits(_batch, _pixel);
            _batch.End();
            CompleteDraw(gameTime);
            return;
        }
        var slideOffset = _panelSlideTransition.Offset(
            _screens.Current, gameTime.TotalGameTime, _slidePanels);
        if (_screens.Current == ClientScreen.Gang && _state is not null)
        {
            var fixedTransform = VirtualInput.Transform(viewport);
            _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: fixedTransform);
            _batch.Draw(_pixel, new Rectangle(0, 0, 640, 460), new Color(8, 10, 12));
            DrawGangDetailsBackdrop(_batch, _pixel, _font, _state);
            _batch.End();

            var panelTransform = Matrix.CreateTranslation(slideOffset, 0, 0) * fixedTransform;
            _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: panelTransform);
            DrawGangDetailsPanel(_batch, _pixel, _font, _state);
            _batch.End();

            _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: fixedTransform);
            DrawScreenOverlays(_batch, _pixel, _font);
            _batch.End();
            CompleteDraw(gameTime);
            return;
        }
        if (!_gameMenuOpen && DrawSeparatedSlidingPanel(viewport, slideOffset))
        {
            DrawBlockingOnlineOverlays(viewport);
            CompleteDraw(gameTime);
            return;
        }
        if (DrawFilteredLastTurnEvents(viewport, slideOffset))
        {
            DrawBlockingOnlineOverlays(viewport);
            CompleteDraw(gameTime);
            return;
        }
        var transform = Matrix.CreateTranslation(slideOffset, 0, 0)
            * VirtualInput.Transform(viewport);
        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
        _batch.Draw(_pixel, new Rectangle(0, 0, 640, 460), new Color(8, 10, 12));
        switch (_screens.Current)
        {
            case ClientScreen.Title:
                DrawTitle(_batch, _pixel, _font);
                break;
            case ClientScreen.Options:
                DrawOptions(_batch, _pixel, _font);
                break;
            case ClientScreen.Help:
                DrawHelp(_batch, _pixel, _font);
                break;
            case ClientScreen.Setup:
                DrawSetup(_batch, _pixel, _font);
                break;
            case ClientScreen.Online:
                DrawOnline(_batch, _pixel, _font);
                break;
            case ClientScreen.Lobby:
                DrawLobby(_batch, _pixel, _font);
                break;
            case ClientScreen.City when _state is not null:
                DrawBoard(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.GameInfo when _state is not null:
                DrawGameInformation(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Endgame when _state?.Outcome is not null:
                DrawEndgame(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Handoff when _state is not null:
                DrawHandoff(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Elimination when _state is not null:
                DrawHotSeatElimination(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.ComlinkView when _state is not null:
                DrawComlinkView(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.ComlinkSend when _state is not null:
                DrawComlinkSend(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Commands when _state is not null:
                DrawCommands(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Hire when _state is not null:
                DrawHire(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Sector when _state is not null:
                DrawSectorDetails(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.SectorGangs when _state is not null:
                DrawSectorGangs(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Site when _state is not null:
                DrawSiteDetails(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.ItemInformation when _state is not null:
                DrawItemDetails(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Finance when _state is not null:
                DrawFinance(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Ranking when _state is not null:
                DrawRanking(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Items when _state is not null:
                DrawItems(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Give when _state is not null:
                DrawGiveEquipment(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Sell when _state is not null:
                DrawSellEquipment(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.CombatSummary when _state is not null:
                DrawCombatResultsPanel(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Search when _state is not null:
                DrawSearch(_batch, _pixel, _font, _state);
                break;
        }
        DrawScreenOverlays(_batch, _pixel, _font);
        _batch.End();
        CompleteDraw(gameTime);
    }

    /// <summary>What sits above every screen: combat playback, the timer, votes, menu, reconnect.</summary>
    private void DrawScreenOverlays(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_state is not null && _combatAnimationPlayer.IsPlaying)
            DrawCombatPanel(batch, pixel, font, _state);
        DrawPlanningTimer(batch, pixel);
        DrawTakeoverVote(batch, pixel, font);
        DrawGameMenu(batch, pixel, font);
        DrawReconnectPopup(batch, pixel, font);
    }
}
