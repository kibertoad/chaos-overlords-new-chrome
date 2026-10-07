using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>
/// FND-SETUP-013: the scenario's title and description at the top of the setup screen's left
/// panel, over a black box.
/// </summary>
public static class SetupScenarioTextLayout
{
    // FND-SETUP-019: 36 characters a line, five lines.
    public const int LineCells = 36;
    public const int LineCount = 5;

    public static Rectangle Box => new(84, 40, 216, 52);

    public static Point Title => new(84, 40);

    public static Point Line(int line) => new(84, 52 + 8 * line);

    /// <summary>
    /// FND-SETUP-019: the description in at most five lines of at most 36 characters. Each line
    /// ends at the last space at or before the 37th character from its start, unless the rest of
    /// the text fits, and the next line starts after that space.
    /// </summary>
    public static IReadOnlyList<string> DescriptionLines(string text)
    {
        var lines = new List<string>(LineCount);
        // The original counts from 1, with the text's last index plus 1 as its end.
        var start = 0;
        var last = false;
        for (var line = 0; line < LineCount && !last; line++)
        {
            var end = start + LineCells;
            if (text.Length < end)
            {
                end = text.Length;
                last = true;
            }
            // A line that ends exactly at the text's end reads past it, which is no space.
            while (!last && end > start && (end >= text.Length || text[end] != ' ')) end--;
            lines.Add(text[start..end]);
            start = end + 1;
        }
        return lines;
    }
}
