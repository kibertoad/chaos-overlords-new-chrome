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
}
