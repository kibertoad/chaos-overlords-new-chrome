using System.Text;

namespace Rechaos.Game;

/// <summary>
/// A single-line text entry, fed by the window's text-input events.
/// </summary>
/// <remarks>
/// The game is otherwise driven by key presses and clicks on the original artwork, and neither can
/// type a server address or a join code. This is the smallest thing that can: it takes characters
/// the platform already decoded (so a non-US keyboard layout works), and it bounds what it accepts
/// so a field cannot become a payload.
/// </remarks>
internal sealed class TextField(string label, int maxLength, string value = "")
{
    private readonly StringBuilder _value = new(value);

    internal string Label { get; } = label;

    internal string Value => _value.ToString();

    /// <summary>Whether typing goes to this field.</summary>
    internal bool IsFocused { get; set; }

    /// <summary>Whether the field draws as dots rather than as what was typed.</summary>
    /// <remarks>
    /// For a lobby password, which is read out loud across a room as often as it is typed in private.
    /// It changes nothing about the value, only what is on screen.
    /// </remarks>
    internal bool IsMasked { get; init; }

    /// <summary>
    /// Takes one character the window decoded.
    /// </summary>
    /// <remarks>
    /// Control characters are dropped rather than inserted: backspace and enter arrive here as
    /// characters too, and neither is text. Backspace deletes; everything else below space is
    /// nothing a field should hold.
    /// </remarks>
    internal void Type(char character)
    {
        if (!IsFocused) return;
        if (character == '\b')
        {
            if (_value.Length > 0) _value.Length--;
            return;
        }
        if (character < ' ' || character == (char)127) return;
        if (_value.Length >= maxLength) return;
        _value.Append(character);
    }

    internal void Set(string text)
    {
        _value.Clear();
        _value.Append(text.Length > maxLength ? text[..maxLength] : text);
    }

    /// <summary>The value with a caret, when focused, for drawing.</summary>
    internal string Display
    {
        get
        {
            var shown = IsMasked ? new string('*', _value.Length) : Value;
            return IsFocused ? $"{shown}_" : shown;
        }
    }
}
