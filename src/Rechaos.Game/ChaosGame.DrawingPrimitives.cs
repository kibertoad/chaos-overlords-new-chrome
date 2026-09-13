using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

/// <summary>
/// The three shapes every panel is drawn out of.
/// </summary>
/// <remarks>
/// Centred text, a button, and a rectangle's outline: small enough to be inlined anywhere and
/// therefore exactly the things that must not be, because a panel that draws its own border is a
/// panel whose border drifts from the rest. They sit apart from the panels that call them.
/// </remarks>
public sealed partial class ChaosGame
{
    private static void DrawCentered(
        PixelFont font, SpriteBatch batch, string text, int y, Color color, int scale)
    {
        var width = text.Length * 6 * scale;
        font.Draw(batch, text, new Vector2((VirtualInput.Width - width) / 2, y), color, scale);
    }

    private static void DrawButton(
        SpriteBatch batch, Texture2D pixel, PixelFont font,
        Rectangle rectangle, string text, bool prominent)
    {
        var fill = prominent ? new Color(72, 54, 18, 235) : new Color(24, 37, 39, 235);
        var border = prominent ? Color.Gold : new Color(100, 125, 112);
        batch.Draw(pixel, rectangle, fill);
        DrawBorder(batch, pixel, rectangle, border, prominent ? 2 : 1);
        var x = rectangle.X + (rectangle.Width - text.Length * 6) / 2;
        var y = rectangle.Y + (rectangle.Height - 7) / 2;
        font.Draw(batch, text, new Vector2(x, y), Color.White, 1);
    }

    private static void DrawBorder(
        SpriteBatch batch, Texture2D pixel, Rectangle rectangle, Color color, int thickness)
    {
        batch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        batch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        batch.Draw(pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        batch.Draw(pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
