using Microsoft.Xna.Framework;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

/// <summary>
/// A request to show a saved match as a new game's first planning entry and write the drawing
/// area to a file, for tests that compare the rebuild's screens with captures of the original.
/// </summary>
/// <param name="SavePath">A native save of a match standing at its first planning entry.</param>
/// <param name="OutputPath">The 640-by-460, 32-bit, top-down bitmap to write.</param>
/// <param name="MarkerFrame">
/// The frame of the Overlord bar's marker the capture showed (FND-UI-038), in place of the one
/// the clock gives.
/// </param>
public sealed record ReferenceFrameRequest(string SavePath, string OutputPath, int? MarkerFrame = null);

public sealed partial class ChaosGame
{
    // The frames drawn before the capture, so textures and layouts loaded on the first draw have
    // settled.
    private const int ReferenceFrameWarmUpDraws = 3;

    private readonly ReferenceFrameRequest? _referenceFrame;
    private int _referenceFrameDraws = -1;

    /// <summary>
    /// Enters the saved match once and then takes the update loop over, so no input and no clock
    /// moves the screen while the frame is drawn.
    /// </summary>
    private bool UpdateReferenceFrame()
    {
        if (_referenceFrame is null) return false;
        if (_referenceFrameDraws < 0 && _definitions is not null)
        {
            EnterNewMatch(NativeSaveStore.Load(_referenceFrame.SavePath, _definitions), advanceToPlanning: false);
            _referenceFrameDraws = 0;
        }
        return true;
    }

    private void CaptureReferenceFrame()
    {
        if (_referenceFrame is null || _referenceFrameDraws < 0) return;
        if (++_referenceFrameDraws < ReferenceFrameWarmUpDraws) return;
        var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var pixels = new Color[checked(width * height)];
        GraphicsDevice.GetBackBufferData(pixels);
        var bgra = new byte[VirtualInput.Width * VirtualInput.Height * 4];
        for (var y = 0; y < VirtualInput.Height && y < height; y++)
        for (var x = 0; x < VirtualInput.Width && x < width; x++)
        {
            var colour = pixels[y * width + x];
            var at = (y * VirtualInput.Width + x) * 4;
            bgra[at] = colour.B;
            bgra[at + 1] = colour.G;
            bgra[at + 2] = colour.R;
        }
        using (var stream = File.Create(_referenceFrame.OutputPath))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(54 + bgra.Length);
            writer.Write(0);
            writer.Write(54);
            writer.Write(40);
            writer.Write(VirtualInput.Width);
            writer.Write(-VirtualInput.Height);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(0);
            writer.Write(bgra.Length);
            writer.Write(0L);
            writer.Write(0L);
            writer.Write(bgra);
        }
        Exit();
    }
}
