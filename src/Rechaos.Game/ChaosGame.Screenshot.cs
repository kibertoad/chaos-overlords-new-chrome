using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private string _screenshotFolder = string.Empty;
    private bool _screenshotRequested;

    private void ConfigureScreenshotOutput(string? screenshotFolder)
    {
        _screenshotFolder = screenshotFolder is null
            ? Path.Combine(AppContext.BaseDirectory, "screenshots")
            : Path.GetFullPath(screenshotFolder);
    }

    private void CompleteDraw(GameTime gameTime)
    {
        base.Draw(gameTime);
        CaptureRequestedScreenshot();
    }

    private void CaptureRequestedScreenshot()
    {
        if (!_screenshotRequested) return;

        _screenshotRequested = false;
        Directory.CreateDirectory(_screenshotFolder);
        var path = Path.Combine(_screenshotFolder,
            $"chaos-overlords-new-chrome-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.png");
        var width = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = GraphicsDevice.PresentationParameters.BackBufferHeight;
        var pixels = new Color[checked(width * height)];
        GraphicsDevice.GetBackBufferData(pixels);
        using var texture = new Texture2D(GraphicsDevice, width, height, false, SurfaceFormat.Color);
        texture.SetData(pixels);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        texture.SaveAsPng(stream, width, height);
        Window.Title = $"Chaos Overlords: New Chrome - screenshot saved: {Path.GetFileName(path)}";
    }
}
