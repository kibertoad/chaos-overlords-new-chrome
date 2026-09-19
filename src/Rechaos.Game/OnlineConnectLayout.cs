using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The four screens reached from ONLINE PLAY: the connect form, the browser, the unfinished
/// sessions and the seat picker.
/// </summary>
/// <remarks>
/// They share the frame in <see cref="OnlineScreenLayout"/> and differ only in what fills it. The
/// form itself reads top to bottom in the order the questions matter: what the player wants to do,
/// who they are, which session, its password, and last the server — which almost nobody changes and
/// which used to be the first thing the screen asked for.
/// </remarks>
public static class OnlineConnectLayout
{
    // The form, group by group. Each group's caption is drawn CaptionOffset above the first control
    // in it, so the numbers below are also the gaps between the groups.

    /// <summary>Hosting or joining, which decides what the rest of the form asks for.</summary>
    public static Rectangle HostRole => OnlineScreenLayout.Half(0, 84, 26);
    public static Rectangle JoinRole => OnlineScreenLayout.Half(1, 84, 26);

    /// <summary>The name and the face this player takes to the table.</summary>
    public static Rectangle Name => new(120, 132, 330, 24);

    /// <summary>
    /// The overlord face this player takes into the session, beside the name they take with it.
    /// </summary>
    /// <remarks>
    /// Here rather than in the lobby because it is the player's own choice and not the session's:
    /// the lobby's settings belong to the host, and a face chosen after the seat was claimed would
    /// have to be sent again and read back by everyone. The name is the other thing a player brings,
    /// so the two are set in the same row, and the name field gives up the width the picker needs.
    /// </remarks>
    public static Rectangle PortraitPrevious => new(456, 132, 14, 32);
    public static Rectangle Portrait => new(472, 132, 32, 32);
    public static Rectangle PortraitNext => new(506, 132, 14, 32);

    /// <summary>Which session: a code to join one, or the listing a hosted one gets.</summary>
    public static Rectangle JoinCode => new(120, 186, 300, 26);
    public static Rectangle PasteJoinCode => new(428, 186, 92, 26);

    /// <summary>
    /// The host's choice of listing, in the row a joining player reads a code into.
    /// </summary>
    /// <remarks>
    /// A pair, like the role above it, because it is the same kind of choice and the screen already
    /// teaches that a lit button is the one in force. It is the one lobby setting that has to be
    /// made here: everything else about the session can be changed in the lobby, where there is
    /// room to read it, and a password can only be set when the lobby is created.
    /// </remarks>
    public static Rectangle PublicChoice => OnlineScreenLayout.Half(0, 186, 26);
    public static Rectangle PrivateChoice => OnlineScreenLayout.Half(1, 186, 26);

    public static Rectangle Password => new(120, 234, 400, 24);

    /// <summary>
    /// The server, last on the form and read as one row.
    /// </summary>
    /// <remarks>
    /// Which service on the left and where it is on the right, because the address is what the
    /// choice selects rather than a separate question. <see cref="Server"/> holds the address
    /// either way: the custom service makes a field of it, and the central one draws its own
    /// address there as text, because nobody may edit that and a box only invited the attempt.
    /// </remarks>
    public static Rectangle Central => new(120, 280, 92, 26);
    public static Rectangle Custom => new(216, 280, 92, 26);
    public static Rectangle Server => new(320, 280, 200, 26);

    /// <summary>
    /// The line the server's health is reported on, which is the server group's caption line.
    /// </summary>
    /// <remarks>
    /// Right-aligned opposite the word SERVER rather than adrift at the foot of the screen: whether
    /// the service is answering is a fact about the row beneath it, and a player who has just
    /// pointed the game at their own machine should not have to hunt for the answer.
    /// </remarks>
    public const int ServerStatusY = 280 - OnlineScreenLayout.CaptionOffset;

    /// <summary>The other ways into a game, which are not this form's action.</summary>
    public static Rectangle Discover => OnlineScreenLayout.Nav(0);
    public static Rectangle Reconnect => OnlineScreenLayout.Nav(1);

    /// <summary>The form's own action, and the way back to the title screen.</summary>
    public static Rectangle Continue => OnlineScreenLayout.Action(0);
    public static Rectangle Back => OnlineScreenLayout.Action(1);

    /// <summary>The unfinished sessions and the seat picker, which are one list and two buttons.</summary>
    public const int HistoryTop = 92;
    public static Rectangle HistoryRow(int index) =>
        OnlineScreenLayout.ListRow(HistoryTop, index);

    /// <summary>The line under the list, for what the selected row cannot do and why.</summary>
    public const int HistoryNoteY = 350;

    public static Rectangle HistoryRejoin => OnlineScreenLayout.Action(0);
    public static Rectangle HistoryBack => OnlineScreenLayout.Action(1);

    /// <summary>The browser: three filters, a list, and three buttons on the filters' own columns.</summary>
    public static Rectangle DiscoveryStatus => OnlineScreenLayout.Third(0, 84, 26);
    public static Rectangle DiscoveryScenario => OnlineScreenLayout.Third(1, 84, 26);
    public static Rectangle DiscoveryAi => OnlineScreenLayout.Third(2, 84, 26);

    public const int DiscoveryTop = 122;
    public static Rectangle DiscoveryRow(int index) =>
        OnlineScreenLayout.ListRow(DiscoveryTop, index);

    public static Rectangle DiscoveryJoin => OnlineScreenLayout.ThirdAction(0);
    public static Rectangle DiscoveryRefresh => OnlineScreenLayout.ThirdAction(1);
    public static Rectangle DiscoveryBack => OnlineScreenLayout.ThirdAction(2);

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

    /// <summary>
    /// The form's text fields, in the order they are read and tabbed through.
    /// </summary>
    /// <remarks>
    /// Top to bottom, which is also the order the caret moves in: a tab order that disagrees with
    /// the screen sends the caret somewhere the eye is not.
    /// </remarks>
    public static IReadOnlyList<Rectangle> Fields => [Name, JoinCode, Password, Server];
}
