using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The lobby screen: the code and the roster on the left, and the settings the host can still change
/// on the right.
/// </summary>
/// <remarks>
/// Everything but the password can be changed while the match has not started, and the lobby is
/// where there is room to lay it out, so the connect screen asks only for what it must: the server,
/// the listing, and a password, which is fixed when the lobby is created.
///
/// The two columns are read independently — one says who is here, the other says what they are about
/// to play — so each carries its own captions and neither runs under the other. The frame around
/// them, and the row of actions that closes the screen, are <see cref="OnlineScreenLayout"/>'s, so
/// arriving here from the connect screen does not move the buttons.
/// </remarks>
public static class OnlineLobbyLayout
{
    /// <summary>The column the code and the roster share.</summary>
    public const int RosterLeft = OnlineScreenLayout.ContentLeft;
    public const int RosterRight = 300;

    /// <summary>The column the settings share, opposite the roster.</summary>
    public const int SettingsLeft = 320;
    public const int SettingsWidth = OnlineScreenLayout.ContentRight - SettingsLeft;

    /// <summary>
    /// How much of a player's name the roster shows.
    /// </summary>
    /// <remarks>
    /// A name can be 32 characters, which is wider than the roster column: the rest would be drawn
    /// under the settings beside it.
    /// </remarks>
    public const int RosterNameColumns = 20;

    /// <summary>The join code, drawn at double height because it is what the host reads out.</summary>
    public const int JoinCodeCaptionY = OnlineScreenLayout.BodyTop;
    public const int JoinCodeY = 84;
    public static Rectangle CopyCode => new(228, 80, 72, 24);

    /// <summary>
    /// The face beside one roster row, with the name drawn to the right of it.
    /// </summary>
    /// <remarks>
    /// Sized to the row rather than the art: six seats and the count line under them share this
    /// column with the code above it, so the face is drawn at half the atlas's 32 pixels.
    /// </remarks>
    public const int RosterCaptionY = 116;
    public static Rectangle RosterPortrait(int row) => new(RosterLeft, 128 + row * 20, 16, 16);

    /// <summary>The line telling the player what the lobby is waiting for.</summary>
    /// <remarks>
    /// On the line the unfinished sessions put their note on, so the two screens close the same way.
    /// </remarks>
    public const int WaitingHintY = OnlineConnectLayout.HistoryNoteY;

    public static Rectangle SessionName => new(SettingsLeft, 84, SettingsWidth, 24);
    public static Rectangle PublicChoice => new(SettingsLeft, 132, 98, 26);
    public static Rectangle PrivateChoice => new(422, 132, 98, 26);
    public static Rectangle LateJoinAllowed => new(SettingsLeft, 182, 98, 26);
    public static Rectangle LateJoinRefused => new(422, 182, 98, 26);

    /// <summary>
    /// What the match is, read by everyone in the lobby rather than only by its host.
    /// </summary>
    /// <remarks>
    /// One line per setting, label left and value right, under the settings that are changed here.
    /// See <see cref="OnlineLobbySummary"/> for what they say.
    /// </remarks>
    public const int SummaryCaptionY = 220;
    public const int SummaryRowPitch = 14;
    public static Rectangle SummaryRow(int index) =>
        new(SettingsLeft, 232 + index * SummaryRowPitch,
            SettingsWidth, OriginalFontLayout.GlyphHeight);

    /// <summary>
    /// The way to the scenario and difficulty, which is where the rest of the settings are.
    /// </summary>
    /// <remarks>
    /// Under the summary it changes rather than beside START, where it read as a second way to begin
    /// the match. What it opens is a screen, not an action on this one.
    /// </remarks>
    public static Rectangle Setup => new(SettingsLeft, 290, SettingsWidth, 26);

    public static Rectangle Start => OnlineScreenLayout.Action(0);
    public static Rectangle Leave => OnlineScreenLayout.Action(1);

    /// <summary>The settings a host may change, which a seated player only reads.</summary>
    public static IReadOnlyList<Rectangle> HostSettings =>
        [SessionName, PublicChoice, PrivateChoice, LateJoinAllowed, LateJoinRefused];
}
