using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The Refurbished Dinosaurs logo in the title screen's lower right corner under a "restored by"
/// line (DEV-UI-019). It turns gold while the pointer is on it, and a click on it opens the
/// project's site.
/// </summary>
internal static class TitleDinoLogo
{
    public const string Url = "https://dinorefurb.com/";

    /// <summary>The logo, at the size of its bitmap, clear of the button row above it and of the
    /// copyright line below.</summary>
    public static readonly Rectangle Bounds = new(VirtualInput.Width - 144 - 6, 412, 144, 32);

    public const string Caption = "RESTORED BY";

    /// <summary>The line over the logo, from its left edge.</summary>
    public static readonly Rectangle CaptionBounds = new(Bounds.X, Bounds.Y - 11,
        Caption.Length * OriginalFontLayout.CellWidth, OriginalFontLayout.LineHeight);

    /// <summary>The grey of the title's other added text, so the logo sits back until the pointer
    /// finds it.</summary>
    public static readonly Color Rest = new(185, 195, 195);

    /// <summary>The gold of the title buttons' borders.</summary>
    public static readonly Color Highlight = Color.Gold;

    public static Color Tint(Point? pointer) =>
        pointer is { } point && Bounds.Contains(point) ? Highlight : Rest;
}
