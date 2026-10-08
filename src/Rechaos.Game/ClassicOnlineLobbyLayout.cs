using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// Modern online controls placed over the original host-lobby sheet <c>PX00144</c>.
/// </summary>
/// <remarks>
/// The original art and its four legacy transport seats are presentation only. These rectangles
/// deliberately use the sheet's existing choice and action faces, but dispatch to the modern
/// public-lobby and join-key operations; no original transport protocol is revived.
/// </remarks>
public static class ClassicOnlineLobbyLayout
{
    public static Rectangle SessionName => new(78, 30, 232, 64);
    public static Rectangle CopyCode => new(240, 67, 64, 20);

    public static Rectangle PublicChoice => new(80, 143, 108, 27);
    public static Rectangle PrivateChoice => new(193, 143, 108, 27);
    public static Rectangle LateJoinAllowed => new(80, 174, 108, 27);
    public static Rectangle LateJoinRefused => new(193, 174, 108, 27);
    /// <summary>
    /// Whether the match can be watched, on the sheet's next pair of faces under the late-join
    /// choice, and the delay on the pair under that.
    /// </summary>
    public static Rectangle WatchRefused => new(80, 209, 108, 27);
    public static Rectangle WatchAllowed => new(193, 209, 108, 27);
    public static Rectangle WatchSooner => new(80, 244, 108, 27);
    public static Rectangle WatchLater => new(193, 244, 108, 27);

    public static Rectangle Setup => new(359, 252, 102, 26);

    /// <summary>Who is watching, on the face beside RULES that the sheet labels REMOVE.</summary>
    public static Rectangle Spectators => new(466, 252, 102, 26);
    public static Rectangle Start => new(359, 370, 102, 48);
    public static Rectangle Leave => new(466, 370, 102, 48);

    /// <summary>
    /// The lobby chat, over the sheet's lower-left panel: its AI mentality and turn limit choices
    /// are set on the rules screen online, so the panel is free.
    /// </summary>
    public static Rectangle ChatLog => new(80, 322, 224, 100);
    public static Rectangle ChatInput => new(80, 425, 224, 16);

    public static Rectangle Roster => new(385, 111, 160, 130);
    public static Rectangle RosterPortrait(int row) => new(390, 119 + row * 19, 16, 16);

    /// <summary>The name beside one roster row, which its own player clicks to change.</summary>
    public static Rectangle RosterName(int row) => new(410, 119 + row * 19, 132, 16);
}
