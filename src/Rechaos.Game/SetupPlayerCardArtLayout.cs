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
