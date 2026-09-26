using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// The modal shown while a running online match reconnects on its own: a log of the recent
/// attempts, each with a button that copies that attempt's full error, and a way to give up.
/// </summary>
public static class ReconnectPopupLayout
{
    public static Rectangle Panel => new(82, 82, 476, 316);

    /// <summary>How many attempts the log keeps; the oldest goes when another arrives.</summary>
    public const int MaxRows = 6;

    public const int RowTop = 184;
    public const int RowPitch = 22;
    public const int TextLeft = 104;

    /// <summary>Where one attempt's line is written.</summary>
    public static Vector2 RowText(int index) => new(TextLeft, RowTop + index * RowPitch);

    /// <summary>The button beside one attempt's line that copies its full error.</summary>
    public static Rectangle CopyError(int index) =>
        new(462, RowTop - 5 + index * RowPitch, 84, 18);

    /// <summary>
    /// The longest line that still ends clear of its copy button, in the font's fixed cells.
    /// </summary>
    public const int SummaryColumns = (462 - 8 - TextLeft) / OriginalFontLayout.CellWidth;

    /// <summary>Where the result of the last copy is reported.</summary>
    public const int CopyStatusTop = 330;

    public static Rectangle StopRetrying => new(222, 354, 196, 28);

    /// <summary>Which attempt's copy button is under the pointer, of the ones drawn.</summary>
    public static int? CopyErrorAt(Point point, int rows)
    {
        for (var index = 0; index < Math.Min(rows, MaxRows); index++)
        {
            if (CopyError(index).Contains(point)) return index;
        }
        return null;
    }
}
