using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The frame the whole online flow is drawn in.
/// </summary>
/// <remarks>
/// Connecting, browsing, picking an unfinished session, taking over a computer empire and sitting in
/// a lobby are five screens a player walks through in a row, and the eye should only have to learn
/// them once: the title, the rule under it, the content column, the status line and the row of
/// actions stand in the same place on every one of them. The numbers live here rather than in each
/// screen's own layout so that the frame moves as one thing, and so that a screen's layout says only
/// what is particular to that screen.
/// </remarks>
public static class OnlineScreenLayout
{
    /// <summary>The panel every online screen fills, centred on the virtual screen.</summary>
    public static Rectangle Panel => new(96, 20, 448, 420);

    /// <summary>The column every screen lays its content out in, inside the panel's margin.</summary>
    public const int ContentLeft = 120;
    public const int ContentRight = 520;
    public const int ContentWidth = ContentRight - ContentLeft;

    /// <summary>The title, and the rule that closes the header under it.</summary>
    public const int TitleY = 34;
    public const int HeaderRuleY = 60;

    /// <summary>The first caption line of a screen's own content, under the header rule.</summary>
    public const int BodyTop = 72;

    /// <summary>
    /// The footer: a rule, the status line, and the row of actions under both.
    /// </summary>
    /// <remarks>
    /// Fixed for every screen, and reserved whether or not there is anything to say, so that a
    /// status arriving does not shift the buttons out from under the pointer. Before this the
    /// status was drawn wherever each screen had room left, which on two of them was through the
    /// panel's own bottom border.
    /// </remarks>
    public const int FooterRuleY = 374;
    public const int StatusY = 382;
    public const int ActionY = 398;
    public const int ActionHeight = 32;

    /// <summary>The row above the footer rule, for the ways off a screen that are not its action.</summary>
    public const int NavY = 330;
    public const int NavHeight = 26;

    /// <summary>How far above a control its caption is drawn.</summary>
    /// <remarks>
    /// One offset for every caption in the flow, whatever it names. A caption is
    /// <see cref="OriginalFontLayout.GlyphHeight"/> tall, so this also fixes the five pixels of air
    /// beneath it, and whatever stands above has to end clear of the whole strip or its border runs
    /// through the words.
    /// </remarks>
    public const int CaptionOffset = 12;

    /// <summary>The pitch and height of a row in any of the flow's lists.</summary>
    /// <remarks>
    /// Two lines of text and the padding around them, which is what a session row carries: a name
    /// and what is being played above, and who is in it and when it was last touched below.
    /// </remarks>
    public const int ListRowHeight = 36;
    public const int ListRowPitch = 42;

    /// <summary>How many rows of a list a screen shows before it scrolls.</summary>
    public const int ListRows = 6;

    /// <summary>One half of a row split in two across the content column.</summary>
    public static Rectangle Half(int index, int y, int height) =>
        new(ContentLeft + index * 210, y, 190, height);

    /// <summary>One third of a row split in three across the content column.</summary>
    public static Rectangle Third(int index, int y, int height) =>
        new(ContentLeft + index * 137, y, 126, height);

    /// <summary>The left and right halves of the row of actions that closes a screen.</summary>
    public static Rectangle Action(int index) => Half(index, ActionY, ActionHeight);

    /// <summary>The same row, split in three, for a screen with one action more than two.</summary>
    public static Rectangle ThirdAction(int index) => Third(index, ActionY, ActionHeight);

    /// <summary>The row above the footer, for browsing away from a screen rather than acting on it.</summary>
    public static Rectangle Nav(int index) => Half(index, NavY, NavHeight);

    /// <summary>A rule drawn the width of the content column.</summary>
    public static Rectangle Rule(int y) => new(ContentLeft, y, ContentWidth, 1);

    /// <summary>One row of a list that starts at the given line.</summary>
    public static Rectangle ListRow(int top, int index) =>
        new(ContentLeft, top + index * ListRowPitch, ContentWidth, ListRowHeight);

    /// <summary>Where a control's caption goes, which is the same place for every one of them.</summary>
    public static Vector2 CaptionAt(Rectangle bounds) =>
        new(bounds.X, bounds.Y - CaptionOffset);
}
