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
        }
    }

    /// <summary>
    /// Draws the lightening of a running flash of <paramref name="kind"/> into the screen batch.
    /// Each screen calls it after the image the flash copies and before the labels, frame and
    /// meter, which FND-UI-037 draws over the lightened copy (RULE-TIMER-004).
    /// </summary>
    private void DrawFlashLightening(SpriteBatch batch, TickedPresentationKind kind)
    {
        if (_tickedPresentation.Source is not null || _tickedPresentation.Kind != kind
            || !_tickedPresentation.Lit(_inputTime)) return;
        // FND-UI-037: white through bitmap 143 from the lightened area's corner, every other pixel
        // white, with the outline the scratch surface's black pen draws through the same pattern.
        var lit = TickedPresentation.LitArea(kind, _tickedPresentation.Area);
        var size = new Point(lit.Width, lit.Height);
        if (!_flashOverlays.TryGetValue(size, out var overlay))
        {
            // The outline lies on the lightened area's own edges, so each size has its own fill.
            overlay = new Texture2D(GraphicsDevice, lit.Width, lit.Height);
            overlay.SetData(OriginalPatternMask.ShadedRectangle(
                TickedPresentation.FlashPattern, lit.Width, lit.Height, Color.White, Color.Black));
            _flashOverlays[size] = overlay;
        }
        batch.Draw(overlay, lit, Color.White);
    }

    private readonly Dictionary<Point, Texture2D> _flashOverlays = [];
}
