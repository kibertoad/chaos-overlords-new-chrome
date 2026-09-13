using System.Text;

namespace Rechaos.Game;

/// <summary>
/// The free-form box a bug report is written in.
/// </summary>
/// <remarks>
/// <para>
/// Not <see cref="TextField"/>, which is one line for a server address, and not
/// <see cref="ComlinkTextEditor"/>, which is the original's fixed grid and folds everything to the
/// original font's uppercase alphabet. A bug report is prose a person writes for another person to
/// read, so it wraps on word boundaries, keeps the case it was typed in, takes newlines, and is long
/// enough to describe something properly.
/// </para>
/// <para>
/// Wrapping is computed rather than stored: the value is exactly what was typed, and the lines are a
/// view of it. That keeps what is sent identical to what the player wrote — a report that had line
/// breaks inserted into it by a text box would read as though they meant something.
/// </para>
/// </remarks>
public sealed class BugReportTextEditor
{
    /// <summary>
    /// Matches <c>BUG_REPORT_LIMITS.messageLength</c>, so the box fills up rather than the server
    /// refusing what somebody spent five minutes writing.
    /// </summary>
    public const int MaximumCharacters = 4_000;

    private readonly StringBuilder _value = new();

    public string Value => _value.ToString();

    public bool IsEmpty => _value.ToString().Trim().Length == 0;

    public bool IsFull => _value.Length >= MaximumCharacters;

    /// <summary>Takes one character the window decoded, or a backspace.</summary>
    public void Type(char character)
    {
        if (character == '\b')
        {
            if (_value.Length > 0) _value.Length--;
            return;
        }
        // Carriage return is what the platform reports for Enter; tab and the rest of the control
        // range are not text a report needs.
        if (character is '\r' or '\n')
        {
            Append('\n');
            return;
        }
        if (character < ' ' || character == (char)127) return;
        Append(character);
    }

    public void Clear() => _value.Clear();

    /// <summary>
    /// The value laid out in a box of the given size, with a caret when focused.
    /// </summary>
    /// <remarks>
    /// Long words are broken rather than allowed to run off the panel: a player pasting a path or a
    /// stack frame into the box is exactly the case where the text matters most.
    /// </remarks>
    public IReadOnlyList<string> DisplayLines(int columns, int rows, bool focused)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        var lines = Wrap(focused ? Value + '_' : Value, columns);
        // The tail is what somebody typing is looking at, so a report longer than the box scrolls
        // with the caret rather than showing its opening paragraph forever.
        var start = Math.Max(0, lines.Count - rows);
        return lines.Skip(start).Take(rows).ToArray();
    }

    /// <summary>Greedy word wrap, honouring the newlines the player typed.</summary>
    public static IReadOnlyList<string> Wrap(string text, int columns)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' '))
            {
                var remaining = word;
                // A word longer than the box is cut at the margin, repeatedly, so it stays visible.
                while (remaining.Length > columns)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    lines.Add(remaining[..columns]);
                    remaining = remaining[columns..];
                }
                if (line.Length > 0 && line.Length + 1 + remaining.Length > columns)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(remaining);
            }
            lines.Add(line.ToString());
        }
        return lines;
    }

    private void Append(char character)
    {
        if (_value.Length >= MaximumCharacters) return;
        _value.Append(character);
    }
}
