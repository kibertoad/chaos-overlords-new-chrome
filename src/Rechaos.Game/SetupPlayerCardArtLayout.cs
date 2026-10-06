using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class SetupPlayerCardArtLayout
{
    /// <summary>
    /// The six colour records the cards' bars are filled with, red, green, blue, yellow, magenta
    /// and cyan at full strength (EXP-UI-015).
    /// </summary>
    public static IReadOnlyList<Color> Colours { get; } =
    [
        new(255, 0, 0), new(0, 255, 0), new(0, 0, 255), new(255, 255, 0), new(255, 0, 255), new(0, 255, 255),
    ];

    /// <summary>FND-SETUP-014: the origin of slot <paramref name="player"/>'s card.</summary>
    public static Point CardOrigin(int player) => new(385 + 83 * (player % 2), 95 + 74 * (player / 2));

    /// <summary>FND-SETUP-014: the 9-by-41 bar in the slot's colour at the card's left edge.</summary>
    public static Rectangle ColourBar(int player)
    {
        var origin = CardOrigin(player);
        return new Rectangle(origin.X, origin.Y, 9, 41);
    }

    /// <summary>
    /// FND-SETUP-014: the name is centred on x 45 of the card, starting at <c>45 - 3 * length</c>,
    /// on the card's row 58.
    /// </summary>
    public static Point NameStart(int player, int length)
    {
        var origin = CardOrigin(player);
        return new Point(origin.X + 45 - 3 * length, origin.Y + 58);
    }

    /// <summary>
    /// DEV-SETUP-003: the half-period of the name editor's caret blink. The edit control blinks
    /// at the blink time the player set in Windows (SRC-WIN32-CARETS); the rebuild has no such
    /// setting and takes 530 ms.
    /// </summary>
    public const int NameCaretBlinkMilliseconds = 530;

    /// <summary>
    /// DEV-SETUP-003: whether the caret shows <paramref name="sinceShown"/> after it was last
    /// placed. It shows at once and then turns on and off every blink time.
    /// </summary>
    public static bool NameCaretShown(TimeSpan sinceShown) =>
        sinceShown < TimeSpan.Zero
        || (long)(sinceShown.TotalMilliseconds / NameCaretBlinkMilliseconds) % 2 == 0;

    /// <summary>
    /// DEV-SETUP-003: the name editor's caret before cell <paramref name="cell"/> of a shown name
    /// <paramref name="length"/> characters long. The edit control's caret inverts the pixels of
    /// a rectangle (SRC-WIN32-CARETS) whose size no entry records; on the card it is a line one
    /// pixel wide in the pixel column left of the cell, from the row above the glyphs to the row
    /// below them.
    /// </summary>
    public static Rectangle NameCaret(int player, int length, int cell)
    {
        var start = NameStart(player, length);
        return new Rectangle(start.X + OriginalFontLayout.CellWidth * cell - 1, start.Y - 1, 1,
            OriginalFontLayout.GlyphHeight + 2);
    }

    /// <summary>
    /// DEV-SETUP-003: the cells <paramref name="first"/> to <paramref name="first"/> +
    /// <paramref name="count"/> of a shown name, as the selection's highlight covers them.
    /// </summary>
    public static Rectangle NameCells(int player, int length, int first, int count)
    {
        var start = NameStart(player, length);
        return new Rectangle(start.X + OriginalFontLayout.CellWidth * first, start.Y - 1,
            OriginalFontLayout.CellWidth * count, OriginalFontLayout.GlyphHeight + 2);
    }

    public static Rectangle ArrowOverlaySource => new(220, 138, 64, 62);

    public static Rectangle PortraitDestination(int player)
    {
        var face = PlayerPortraitLayout.SetupLarge(player);
        return new Rectangle(face.X, face.Y + 3, 64, 60);
    }

    public static Rectangle PortraitSource(int portraitId)
    {
        if (portraitId is < 0 or >= PlayerPortraitLayout.Count)
            throw new ArgumentOutOfRangeException(nameof(portraitId));
        return new Rectangle(portraitId * 32, 480, 32, 30);
    }
}
