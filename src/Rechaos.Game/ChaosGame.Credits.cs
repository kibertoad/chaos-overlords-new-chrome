using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

/// <summary>The credits screen (SCR-UI-002, FND-UI-007).</summary>
public static class CreditsLayout
{
    /// <summary>PX00100, copied opaquely over the whole screen; a click anywhere on it closes it.</summary>
    public static Rectangle Screen => new(0, 0, 640, 460);

    /// <summary>
    /// SCR-UI-002: any key closes the screen. A key counts on the frame it goes down, so the key
    /// that opened the screen, still held, does not close it.
    /// </summary>
    public static bool AnyKeyPressed(KeyboardState current, KeyboardState previous) =>
        current.GetPressedKeys().Any(key => !previous.IsKeyDown(key));
}

public sealed partial class ChaosGame
{
    private Texture2D? _creditsBackground;
    private bool _creditsOpen;

    /// <summary>
    /// Opens the credits over whatever screen is showing. The original opens them from its Help
    /// menu's About command on any screen (SCR-UI-002, SCR-UI-009); the rebuild has no menu bar,
    /// so Shift+F1 opens them wherever F1 opens help.
    /// </summary>
    private void OpenCredits()
    {
        _creditsBackground ??= LoadTexture("PX00100.bmp");
        _creditsOpen = true;
    }

    /// <summary>
    /// SCR-UI-002: a click, a press of either button, or any key closes the credits and leaves the
    /// screen they covered as it was. The screen plays no sound.
    /// </summary>
    private void UpdateCredits(KeyboardState keyboard, MouseState mouse)
    {
        var clicked = PointerButtonEdges.Pressed(mouse.LeftButton, _previousMouse.LeftButton)
            || PointerButtonEdges.Pressed(mouse.RightButton, _previousMouse.RightButton);
        if (clicked || CreditsLayout.AnyKeyPressed(keyboard, _previousKeyboard))
            _creditsOpen = false;
    }

    private void DrawCredits(SpriteBatch batch, Texture2D pixel)
    {
        if (_creditsBackground is not null)
            batch.Draw(_creditsBackground, CreditsLayout.Screen, Color.White);
        else
            batch.Draw(pixel, CreditsLayout.Screen, Color.Black);
    }
}
