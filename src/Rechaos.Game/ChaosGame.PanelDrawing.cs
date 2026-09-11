using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
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
}
