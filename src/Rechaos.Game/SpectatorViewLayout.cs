using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// Where the spectator view puts what it adds to the city screen.
/// </summary>
/// <remarks>
/// <para>
/// The view is the city screen's art (SCR-UI-003) with the map, the Overlord bar and the status
/// console drawn as a player's city draws them. The console's command buttons give orders, which a
/// spectator has none of, so a panel covers them: it says what is being watched, how far behind
/// the players it is held, which seat the map is drawn for, and holds the way out.
/// </para>
/// <para>
/// The panel sits inside the button block of the art, clear of the map on its left, the status
/// console above it and the message line under it.
/// </para>
/// </remarks>
public static class SpectatorViewLayout
{
    /// <summary>The panel over the command buttons.</summary>
    public static Rectangle Panel => new(444, 118, 162, 228);

    /// <summary>How many characters of the original font one panel line holds.</summary>
    public const int LineColumns = (162 - 2 * TextInset) / OriginalFontLayout.CellWidth;

    private const int TextInset = 6;

    /// <summary>Where the text starts on each line.</summary>
    public const int TextLeft = 444 + TextInset;

    /// <summary>The top of the nth text line.</summary>
    public static int LineY(int line) => 126 + line * 12;

    /// <summary>How many text lines fit above the seat chooser.</summary>
    public const int TextLines = 8;

    /// <summary>The caption over the seat the map is drawn for.</summary>
    public static int FollowCaptionY => 232;

    /// <summary>The arrow that follows the previous seat in play.</summary>
    public static Rectangle FollowPrevious => new(450, 246, 14, 20);

    /// <summary>The arrow that follows the next seat in play.</summary>
    public static Rectangle FollowNext => new(586, 246, 14, 20);

    /// <summary>The followed seat's name, between the arrows.</summary>
    public static Rectangle FollowName => new(466, 246, 118, 20);

    /// <summary>The button that stops watching.</summary>
    public static Rectangle Leave => new(456, 308, 138, 28);

    /// <summary>The way out while there is no city to draw yet, on the online frame.</summary>
    public static Rectangle WaitingLeave => OnlineScreenLayout.ThirdAction(2);

    /// <summary>The line under the map that says how to work the view.</summary>
    public const string Footer = "WATCHING  TAB NEXT EMPIRE  ARROWS SECTOR  ESC LEAVES";
}

/// <summary>
/// The list of who is watching, drawn over the lobby or the match.
/// </summary>
/// <remarks>
/// Every seat can open it, so nobody plays without knowing who can see the match later; only the
/// host's copy has the remove button lit.
/// </remarks>
public static class SpectatorListLayout
{
    public static Rectangle Panel => new(140, 86, 360, 288);

    /// <summary>How many spectators the list shows at once; it scrolls with the selection.</summary>
    public const int Rows = 9;

    public static int CaptionY => 98;

    public static Rectangle Row(int index) => new(152, 120 + index * 22, 336, 20);

    public static int StatusY => 322;

    public static Rectangle Remove => new(152, 336, 160, 26);

    public static Rectangle Close => new(328, 336, 160, 26);
}
