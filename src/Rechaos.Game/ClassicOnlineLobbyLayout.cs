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
    public static Rectangle Setup => new(359, 252, 102, 26);
    public static Rectangle Start => new(359, 370, 102, 48);
    public static Rectangle Leave => new(466, 370, 102, 48);

    public static Rectangle Roster => new(385, 111, 160, 130);
    public static Rectangle RosterPortrait(int row) => new(390, 119 + row * 19, 16, 16);
}
