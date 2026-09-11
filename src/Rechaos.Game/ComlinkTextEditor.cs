using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public sealed class ComlinkTextEditor
{
    public string Text { get; private set; } = string.Empty;
    public bool IsFull => Text.Length == MatchLimits.ComlinkMessageCharacters;

    public bool TryAppend(char character)
    {
        character = char.ToUpperInvariant(character);
        if (IsFull || character is < OriginalFontLayout.FirstCharacter
            or > OriginalFontLayout.LastCharacter)
            return false;
        Text += character;
        return true;
    }

    public bool Backspace()
    {
        if (Text.Length == 0) return false;
        Text = Text[..^1];
        return true;
    }

    public void Clear() => Text = string.Empty;

    public IReadOnlyList<string> DisplayLines() => DisplayLines(Text);

    public static IReadOnlyList<string> DisplayLines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = new string[ComlinkSendLayout.MessageRows];
        for (var row = 0; row < lines.Length; row++)
        {
            var start = row * ComlinkSendLayout.MessageColumns;
            lines[row] = start >= text.Length
                ? string.Empty
                : text.Substring(start, Math.Min(ComlinkSendLayout.MessageColumns,
                    text.Length - start));
        }
        return lines;
    }
}
