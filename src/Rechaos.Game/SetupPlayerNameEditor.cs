namespace Rechaos.Game;

public sealed class SetupPlayerNameEditor
{
    public const int MaximumCharacters = LocalSetupPolicy.MaximumPlayerNameCharacters;
    public string Text { get; private set; } = string.Empty;
    public bool IsFull => Text.Length == MaximumCharacters;

    public void Begin(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumCharacters)
            throw new ArgumentException($"Player names cannot exceed {MaximumCharacters} characters.",
                nameof(text));
        Text = text.ToUpperInvariant();
    }

    /// <summary>
    /// Types a character the edit control accepted. The control upper-cases letters, and the
    /// name copy turns every character outside space to <c>Z</c> into a space (FND-UI-022,
    /// FND-UI-064, EXP-UI-053). A control character types nothing.
    /// </summary>
    public bool TryAppend(char character)
    {
        if (IsFull || character < ' ') return false;
        character = char.ToUpperInvariant(character);
        if (character is < OriginalFontLayout.FirstCharacter or > OriginalFontLayout.LastCharacter)
            character = ' ';
        Text += character;
        return true;
    }

    public bool Backspace()
    {
        if (Text.Length == 0) return false;
        Text = Text[..^1];
        return true;
    }
}
