using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The lobby screen: the roster on the left, and the settings the host can still change on the
/// right.
/// </summary>
/// <remarks>
/// Everything but the password can be changed while the match has not started, and the lobby is
/// where there is room to lay it out, so the connect screen asks only for what it must: the server,
/// the listing, and a password, which is fixed when the lobby is created.
/// </remarks>
public static class OnlineLobbyLayout
{
    /// <summary>
    /// How much of a player's name the roster shows.
    /// </summary>
    /// <remarks>
    /// A name can be 32 characters, which is wider than the roster column: the rest would be drawn
    /// under the settings beside it.
    /// </remarks>
    public const int RosterNameColumns = 20;

    public static Rectangle CopyCode => new(112, 372, 128, 32);
    public static Rectangle Setup => new(256, 332, 128, 32);
    public static Rectangle Start => new(256, 372, 128, 32);
    public static Rectangle Leave => new(400, 372, 128, 32);

    public static Rectangle SessionName => new(316, 204, 208, 22);
    public static Rectangle PublicChoice => new(316, 254, 102, 24);
    public static Rectangle PrivateChoice => new(422, 254, 102, 24);
    public static Rectangle LateJoinAllowed => new(316, 304, 102, 24);
    public static Rectangle LateJoinRefused => new(422, 304, 102, 24);

    /// <summary>The settings a host may change, which a seated player only reads.</summary>
    public static IReadOnlyList<Rectangle> HostSettings =>
        [SessionName, PublicChoice, PrivateChoice, LateJoinAllowed, LateJoinRefused];
}
