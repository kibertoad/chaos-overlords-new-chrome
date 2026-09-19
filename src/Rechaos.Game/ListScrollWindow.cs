namespace Rechaos.Game;

/// <summary>
/// The slice of a list that a fixed number of rows is showing.
/// </summary>
/// <remarks>
/// Drawing a list and clicking one need the same answer to "which entry is row three", and when the
/// two worked it out separately they drifted: the browser drew five rows and the click that landed
/// on the sixth was read as the fifth. Both now ask this, and the line the screen puts above the
/// list — see <see cref="Tally"/> — is computed from the same numbers, so a list that has more
/// entries than it can show says so rather than silently ending.
/// </remarks>
public readonly record struct ListScrollWindow(int Total, int Offset, int Rows)
{
    /// <summary>The window that keeps the selected entry on the screen.</summary>
    public static ListScrollWindow Of(int total, int selection, int rows)
    {
        var offset = Math.Clamp(selection - rows + 1, 0, Math.Max(0, total - rows));
        return new ListScrollWindow(total, offset, rows);
    }

    /// <summary>How many rows are actually drawn, which is fewer when the list is short.</summary>
    public int VisibleRows => Math.Clamp(Total - Offset, 0, Rows);

    /// <summary>The entry a drawn row stands for.</summary>
    public int IndexAt(int row) => Offset + row;

    /// <summary>Whether anything is out of sight above or below.</summary>
    public bool Scrolls => Total > Rows;

    /// <summary>
    /// Which of how many the list is showing, or how many there are when they all fit.
    /// </summary>
    /// <remarks>
    /// Empty for an empty list, which says nothing worth reading: the list itself says so in the
    /// space where its rows would have been.
    /// </remarks>
    public string Tally(string noun) => Total switch
    {
        0 => string.Empty,
        _ when !Scrolls => $"{Total} {noun}",
        _ => $"{Offset + 1}-{Offset + VisibleRows} OF {Total} {noun}"
    };
}
