using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

/// <summary>
/// The pressed key faces and the cell and site flashes, each of which pauses on the presentation
/// clock before the game takes input again (RULE-TIMER-004).
/// </summary>
public sealed partial class ChaosGame
{
    private readonly TickedPresentation _tickedPresentation = new();

    /// <summary>
    /// <c>fn_00418CCC</c> (FND-UI-019): plays slot 3, shows the pressed face for one tick of the
    /// presentation clock, then acts. Input that arrives during the wait is dropped.
    /// </summary>
    /// <param name="action">
    /// What the key does once the wait ends. It reports its own result with the pointer button's
    /// sounds, since slot 3 has already played.
    /// </param>
    private void PressKeyFace(PressedKeyFace face, Point topLeft, Action action)
    {
        AcceptInput();
        _tickedPresentation.Start(TickedPresentationKind.KeyFace,
            PressedKeyFaces.Destination(face, topLeft), PressedKeyFaces.Source(face), action,
            _inputTime);
    }

    /// <summary>Flashes a city cell, a site portrait or a nine-sector display cell.</summary>
    private void StartFlash(TickedPresentationKind kind, Rectangle area) =>
        _tickedPresentation.Start(kind, area, null, null, _inputTime);

    /// <summary>
    /// Runs the action of a pressed face once its wait is over. Returns whether a step was running
    /// this frame, in which case the frame's input is dropped.
    /// </summary>
    private bool UpdateTickedPresentation()
    {
        if (!_tickedPresentation.Active) return false;
        if (_tickedPresentation.TryFinish(_inputTime, out var then)) then?.Invoke();
        return true;
    }

    private void DrawTickedPresentation(Viewport viewport)
    {
        if (_batch is null || _pixel is null || !_tickedPresentation.Lit(_inputTime)) return;
        var area = _tickedPresentation.Area;
        if (_tickedPresentation.Source is { } source)
        {
            _batch.Begin(samplerState: SamplerState.PointClamp,
                transformMatrix: VirtualInput.Transform(viewport));
            if (_uiSprites is not null) _batch.Draw(_uiSprites, area, source, Color.White);
            else _batch.Draw(_pixel, area, new Color(90, 90, 90));
            _batch.End();
            return;
        }
        // The findings record a lightened copy without its exact colours; an additive grey lifts
        // every channel of the cell by the same amount.
        _batch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp,
            transformMatrix: VirtualInput.Transform(viewport));
        _batch.Draw(_pixel, area, new Color(72, 72, 72));
        _batch.End();
    }
}
