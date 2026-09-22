using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

/// <summary>
/// How loudly a button asks to be pressed, and whether it can be at all.
/// </summary>
/// <remarks>
/// Two states drawn from one boolean could not tell a button that is the point of the screen from
/// one that is merely available, nor either from one that cannot be pressed: a greyed CONNECT and an
/// unchosen CUSTOM were the same picture. Three named states keep the warm fill for the one thing a
/// screen is for, the plain fill for the rest, and a dimmed one that says pressing will do nothing.
/// </remarks>
internal enum ButtonEmphasis
{
    /// <summary>The thing the screen is for. At most one per screen.</summary>
    Primary,

    /// <summary>Available, and secondary to whatever the screen is for.</summary>
    Secondary,

    /// <summary>Nothing will happen, because of how the screen currently stands.</summary>
    Disabled
}

/// <summary>
/// The shapes every panel is drawn out of.
/// </summary>
/// <remarks>
/// Centred text, a button, a chosen-or-not segment, and a rectangle's outline: small enough to be
/// inlined anywhere and therefore exactly the things that must not be, because a panel that draws
/// its own border is a panel whose border drifts from the rest. They sit apart from the panels that
/// call them.
/// </remarks>
public sealed partial class ChaosGame
{
    private static void DrawCentered(
        PixelFont font, SpriteBatch batch, string text, int y, Color color, int scale)
    {
        var width = text.Length * 6 * scale;
        font.Draw(batch, text, new Vector2((VirtualInput.Width - width) / 2, y), color, scale);
    }

    /// <summary>Text whose right edge, rather than its left, is the position given.</summary>
    private static void DrawRightAligned(
        PixelFont font, SpriteBatch batch, string text, int right, int y, Color color)
    {
        var width = text.Length * OriginalFontLayout.CellWidth;
        font.Draw(batch, text, new Vector2(right - width, y), color, 1);
    }

    private static void DrawButton(
        SpriteBatch batch, Texture2D pixel, PixelFont font,
        Rectangle rectangle, string text, bool prominent) =>
        DrawButton(batch, pixel, font, rectangle, text,
            prominent ? ButtonEmphasis.Primary : ButtonEmphasis.Secondary);

    private static void DrawButton(
        SpriteBatch batch, Texture2D pixel, PixelFont font,
        Rectangle rectangle, string text, ButtonEmphasis emphasis)
    {
        var (fill, border, label, thickness) = emphasis switch
        {
            ButtonEmphasis.Primary =>
                (new Color(72, 54, 18, 235), Color.Gold, Color.White, 2),
            ButtonEmphasis.Disabled =>
                (new Color(16, 26, 24, 235), new Color(52, 66, 62), new Color(96, 112, 107), 1),
            _ => (new Color(24, 37, 39, 235), new Color(100, 125, 112), Color.White, 1)
        };
        batch.Draw(pixel, rectangle, fill);
        DrawBorder(batch, pixel, rectangle, border, thickness);
        DrawLabel(batch, font, rectangle, text, label);
    }

    /// <summary>
    /// One segment of a choice, lit when it is the answer in force.
    /// </summary>
    /// <remarks>
    /// Deliberately not the same picture as <see cref="ButtonEmphasis.Primary"/>: the gold fill says
    /// "press this", and a chosen segment is not something to press but something already true. It
    /// borrows the teal a selected list row is drawn in, so one highlight means one thing across the
    /// whole flow.
    /// </remarks>
    private static void DrawChoice(
        SpriteBatch batch, Texture2D pixel, PixelFont font,
        Rectangle rectangle, string text, bool chosen, bool enabled = true)
    {
        var (fill, border, label) = (chosen, enabled) switch
        {
            (true, true) => (new Color(30, 62, 55), Color.Gold, Color.Gold),
            (true, false) => (new Color(24, 44, 40), new Color(120, 104, 52), new Color(170, 150, 84)),
            (false, true) => (new Color(18, 30, 28), new Color(70, 90, 88), new Color(190, 205, 200)),
            _ => (new Color(14, 22, 21), new Color(52, 66, 62), new Color(96, 112, 107))
        };
        batch.Draw(pixel, rectangle, fill);
        DrawBorder(batch, pixel, rectangle, border, 1);
        DrawLabel(batch, font, rectangle, text, label);
    }

    /// <summary>Text centred in a control, both ways.</summary>
    private static void DrawLabel(
        SpriteBatch batch, PixelFont font, Rectangle rectangle, string text, Color color)
    {
        var x = rectangle.X + (rectangle.Width - text.Length * OriginalFontLayout.CellWidth) / 2;
        var y = rectangle.Y + (rectangle.Height - OriginalFontLayout.GlyphHeight) / 2;
        font.Draw(batch, text, new Vector2(x, y), color, 1);
    }

    private static void DrawBorder(
        SpriteBatch batch, Texture2D pixel, Rectangle rectangle, Color color, int thickness)
    {
        Span<Rectangle> strips = stackalloc Rectangle[BorderGeometry.MaximumStrips];
        var count = BorderGeometry.Strips(rectangle, thickness, strips);
        for (var strip = 0; strip < count; strip++) batch.Draw(pixel, strips[strip], color);
    }
}

/// <summary>
/// The strips a hollow rectangle's outline is tiled from.
/// </summary>
/// <remarks>
/// The obvious four rectangles — a full-width band top and bottom, a full-height bar down each
/// side — cover the four corners twice. That is invisible while every border is opaque, because
/// painting a pixel the same colour twice is painting it once, and it stops being invisible the
/// moment a border carries alpha: the corners blend twice and read darker than the edges they
/// join. Shortening the side bars to the rows between the bands costs nothing and makes the
/// coverage exact, which is a property worth stating separately from the drawing code that
/// consumes it.
/// </remarks>
internal static class BorderGeometry
{
    /// <summary>The most strips <see cref="Strips"/> can write.</summary>
    public const int MaximumStrips = 4;

    /// <summary>
    /// Writes the non-overlapping strips covering <paramref name="rectangle"/>'s border, in top,
    /// left, right, bottom order, and returns how many carry pixels. Degenerate rectangles
    /// shorter or narrower than twice <paramref name="thickness"/> stay covered exactly once:
    /// the bands absorb what is left rather than overlapping each other.
    /// </summary>
    public static int Strips(Rectangle rectangle, int thickness, Span<Rectangle> strips)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(thickness);
        if (strips.Length < MaximumStrips) throw new ArgumentException(
            $"Needs room for {MaximumStrips} strips.", nameof(strips));
        // Each band is clamped to what the opposite band has not already taken, so a rectangle
        // thinner than two thicknesses is filled solid rather than blended twice down its middle.
        var topHeight = Math.Min(thickness, rectangle.Height);
        var sidesTop = rectangle.Y + topHeight;
        var bottomTop = Math.Max(sidesTop, rectangle.Bottom - thickness);
        var sidesHeight = bottomTop - sidesTop;
        var leftWidth = Math.Min(thickness, rectangle.Width);
        var rightLeft = Math.Max(rectangle.X + leftWidth, rectangle.Right - thickness);
        var rightWidth = rectangle.Right - rightLeft;
        var count = 0;
        if (topHeight > 0 && rectangle.Width > 0)
            strips[count++] = new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, topHeight);
        if (sidesHeight > 0)
        {
            if (leftWidth > 0)
                strips[count++] = new Rectangle(rectangle.X, sidesTop, leftWidth, sidesHeight);
            if (rightWidth > 0)
                strips[count++] = new Rectangle(rightLeft, sidesTop, rightWidth, sidesHeight);
        }
        if (bottomTop < rectangle.Bottom && rectangle.Width > 0)
            strips[count++] = new Rectangle(
                rectangle.X, bottomTop, rectangle.Width, rectangle.Bottom - bottomTop);
        return count;
    }
}
