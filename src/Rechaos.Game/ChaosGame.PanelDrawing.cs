using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private bool DrawFilteredLastTurnEvents(Viewport viewport, int slideOffset)
    {
        if (_screens.Current != ClientScreen.Events || _state is null
            || _batch is null || _pixel is null || _font is null)
            return false;

        var transform = Matrix.CreateTranslation(slideOffset, 0, 0)
            * VirtualInput.Transform(viewport);
        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
        _batch.Draw(_pixel, new Rectangle(0, 0, 640, 460), new Color(8, 10, 12));
        DrawLastTurnEventsFrame(_batch, _pixel, _font, _state);
        _batch.End();

        var notification = CurrentEventReport(_state);
        if (notification is not null)
        {
            var siteSampler = _smoothEventSiteImages
                ? SamplerState.LinearClamp
                : SamplerState.PointClamp;
            _batch.Begin(samplerState: siteSampler, transformMatrix: transform);
            var drewSiteBackground = DrawInfluenceSiteBackground(_batch, _state, notification);
            if (drewSiteBackground && !_smoothEventSiteImages
                && _eventSiteDitherOverlay is not null)
                _batch.Draw(_eventSiteDitherOverlay, LastTurnEventsLayout.Artwork, Color.White);
            _batch.End();
        }

        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
        DrawLastTurnEventContent(_batch, _pixel, _font, _state, notification);
        DrawButton(_batch, _pixel, _font, LastTurnEventsLayout.Ok, "OK", true);
        if (_combatAnimationPlayer.IsPlaying)
            DrawCombatPanel(_batch, _pixel, _font, _state);
        DrawPlanningTimer(_batch, _pixel);
        DrawGameMenu(_batch, _pixel, _font);
        _batch.End();
        return true;
    }

    private bool DrawSeparatedSlidingPanel(Viewport viewport, int slideOffset)
    {
        if ((_screens.Current is not (ClientScreen.Hire or ClientScreen.Ranking
                or ClientScreen.CombatSummary)) || _state is null
            || _batch is null || _pixel is null || _font is null)
            return false;

        var fixedTransform = VirtualInput.Transform(viewport);
        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: fixedTransform);
        _batch.Draw(_pixel, new Rectangle(0, 0, 640, 460), new Color(8, 10, 12));
        if ((_screens.Current is ClientScreen.Ranking or ClientScreen.CombatSummary)
            && _managementReturnScreen == ClientScreen.Sector)
            DrawSectorDetails(_batch, _pixel, _font, _state);
        else
            DrawBoard(_batch, _pixel, _font, _state);
        _batch.End();

        var panelTransform = Matrix.CreateTranslation(slideOffset, 0, 0) * fixedTransform;
        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: panelTransform);
        switch (_screens.Current)
        {
            case ClientScreen.Hire:
                DrawHirePanel(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.Ranking:
                DrawRankingPanel(_batch, _pixel, _font, _state);
                break;
            case ClientScreen.CombatSummary:
                DrawCombatResultsPanelContent(_batch, _pixel, _font, _state);
                break;
        }
        _batch.End();

        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: fixedTransform);
        if (_combatAnimationPlayer.IsPlaying)
            DrawCombatPanel(_batch, _pixel, _font, _state);
        DrawPlanningTimer(_batch, _pixel);
        _batch.End();
        return true;
    }

    /// <summary>The map a panel opened over: the sector view, or the city board.</summary>
    private void DrawMapBackdrop(
        SpriteBatch batch, Texture2D pixel, PixelFont font, MatchState state, ClientScreen returnScreen)
    {
        if (returnScreen == ClientScreen.Sector) DrawSectorDetails(batch, pixel, font, state);
        else DrawBoard(batch, pixel, font, state);
    }

    /// <summary>A panel's original artwork, or a dark plate where the asset pack lacks it.</summary>
    private static void DrawPanelArtwork(
        SpriteBatch batch, Texture2D pixel, Texture2D? artwork, Rectangle panel, int fallbackAlpha = 245)
    {
        if (artwork is not null) batch.Draw(artwork, panel, Color.White);
        else batch.Draw(pixel, panel, new Color(0, 0, 0, fallbackAlpha));
    }
}
