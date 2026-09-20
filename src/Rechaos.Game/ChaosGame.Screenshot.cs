using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private string _screenshotFolder = string.Empty;
    private bool _screenshotRequested;

    private void ConfigureScreenshotOutput(string? screenshotFolder, string userDataRoot)
    {
        _screenshotFolder = screenshotFolder is null
            ? Path.Combine(userDataRoot, "screenshots")
            : Path.GetFullPath(screenshotFolder);
    }

    private void CompleteDraw(GameTime gameTime)
    {
        base.Draw(gameTime);
        CaptureRequestedScreenshot();
    }

    /// <summary>
    /// Writes the back buffer to a PNG, or says it could not.
    /// </summary>
    /// <remarks>
    /// This runs at the end of <c>Draw</c>, so anything it throws leaves <c>Game.Run</c> and ends
    /// the process with the player's match in it. The folder is the writable user data root rather
    /// than the install directory, because the <c>.deb</c> installs under <c>/opt</c> and the
    /// <c>.pkg</c> under <c>/</c>, both owned by root, where creating <c>screenshots</c> throws.
    /// </remarks>
    private void CaptureRequestedScreenshot()
    {
        if (!_screenshotRequested) return;

        _screenshotRequested = false;
        try
        {
            Directory.CreateDirectory(_screenshotFolder);
            var path = Path.Combine(_screenshotFolder,
                $"chaos-overlords-new-chrome-{DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}.png");
            var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var pixels = new Color[checked(width * height)];
            GraphicsDevice.GetBackBufferData(pixels);
            using var texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
            texture.SetData(pixels);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                texture.SaveAsPng(stream, width, height);
            Window.Title = $"Chaos Overlords: New Chrome - screenshot saved: {Path.GetFileName(path)}";
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException or NotSupportedException
            or InvalidOperationException or OutOfMemoryException)
        {
            _diagnostics?.Write("screenshot.failed", new Dictionary<string, string?>
            {
                ["folder"] = _screenshotFolder,
                ["error"] = exception.Message
            });
            Window.Title = "Chaos Overlords: New Chrome - screenshot failed";
            _message = "SCREENSHOT FAILED";
        }
    }
}
