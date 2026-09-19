using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class OnlineConnectLayout
{
    public static Rectangle Central => new(120, 108, 190, 26);
    public static Rectangle Custom => new(330, 108, 190, 26);
    public static Rectangle Server => new(120, 150, 400, 22);
    public static Rectangle HostRole => new(120, 188, 190, 26);
    public static Rectangle JoinRole => new(330, 188, 190, 26);
    public static Rectangle Name => new(120, 226, 330, 22);

    /// <summary>
    /// The overlord face this player takes into the session, beside the name they take with it.
    /// </summary>
    /// <remarks>
    /// Here rather than in the lobby because it is the player's own choice and not the session's:
    /// the lobby's settings belong to the host, and a face chosen after the seat was claimed would
    /// have to be sent again and read back by everyone. The name is the other thing a player brings,
    /// so the two are set in the same row, and the name field gives up the width the picker needs.
    /// </remarks>
    public static Rectangle PortraitPrevious => new(456, 226, 14, 32);
    public static Rectangle Portrait => new(472, 226, 32, 32);
    public static Rectangle PortraitNext => new(506, 226, 14, 32);
    public static Rectangle JoinCode => new(120, 264, 300, 22);
    public static Rectangle PasteJoinCode => new(428, 260, 92, 30);
    public static Rectangle Password => new(120, 302, 400, 22);

    /// <summary>
    /// How far above a control its caption is drawn.
    /// </summary>
    /// <remarks>
    /// One offset for every caption on the screen, whatever it names. A caption is
    /// <see cref="OriginalFontLayout.GlyphHeight"/> tall, so this also fixes the gap beneath it, and
    /// the control above has to end clear of the whole strip or its border runs through the words.
    /// </remarks>
    public const int CaptionOffset = 12;

    /// <summary>
    /// The host's choice of listing, in the row a joining player reads a code into.
    /// </summary>
    /// <remarks>
    /// A pair, like the service and the role above it, because it is the same kind of choice and the
    /// screen already teaches that a lit button is the one in force. It is the one lobby setting
    /// that has to be made here: everything else about the session can be changed in the lobby,
    /// where there is room to read it, and a password can only be set when the lobby is created.
    /// </remarks>
    public static Rectangle PublicChoice => new(120, 262, 190, 26);
    public static Rectangle PrivateChoice => new(330, 262, 190, 26);
    public static Rectangle Continue => new(120, 340, 190, 30);
    public static Rectangle Discover => new(330, 340, 190, 30);
    public static Rectangle Reconnect => new(120, 378, 190, 30);
    public static Rectangle Back => new(330, 378, 190, 30);
    public static Rectangle HistoryRejoin => new(120, 382, 190, 30);
    public static Rectangle HistoryBack => new(330, 382, 190, 30);
    public static Rectangle HistoryRow(int index) => new(120, 126 + index * 38, 400, 30);
    public static Rectangle DiscoveryStatus => new(120, 112, 126, 28);
    public static Rectangle DiscoveryScenario => new(257, 112, 126, 28);
    public static Rectangle DiscoveryAi => new(394, 112, 126, 28);
    public static Rectangle DiscoveryRow(int index) => new(120, 154 + index * 42, 400, 34);
    public static Rectangle DiscoveryJoin => new(120, 382, 190, 30);
    public static Rectangle DiscoveryBack => new(330, 382, 190, 30);
    public const int ServerStatusY = 416;
    public const int StatusY = 436;
    public static Rectangle ErrorPanel => new(60, 72, 520, 316);
    public static Rectangle CopyError => new(104, 338, 204, 30);
    public static Rectangle DismissError => new(332, 338, 204, 30);

    /// <summary>The height of one row of an open filter dropdown.</summary>
    public const int DiscoveryOptionHeight = 18;

    public static Rectangle DiscoveryFilter(int filter) => filter switch
    {
        DiscoveryFilters.Status => DiscoveryStatus,
        DiscoveryFilters.Scenario => DiscoveryScenario,
        DiscoveryFilters.Ai => DiscoveryAi,
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };

    /// <summary>The area an open filter dropdown covers, hanging off the bottom of its button.</summary>
    public static Rectangle DiscoveryFilterMenu(int filter)
    {
        var anchor = DiscoveryFilter(filter);
        return new Rectangle(
            anchor.X,
            anchor.Bottom,
            anchor.Width,
            DiscoveryFilters.OptionCount(filter) * DiscoveryOptionHeight);
    }

    public static Rectangle DiscoveryFilterOption(int filter, int option)
    {
        var anchor = DiscoveryFilter(filter);
        return new Rectangle(
            anchor.X,
            anchor.Bottom + option * DiscoveryOptionHeight,
            anchor.Width,
            DiscoveryOptionHeight);
    }

    public static IReadOnlyList<Rectangle> Fields => [Server, Name, JoinCode, Password];
}
