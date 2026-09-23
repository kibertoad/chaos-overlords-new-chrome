namespace Rechaos.Core.GameModel;

/// <summary>
/// Projects a modern display name into the original executable's fixed player-name record.
/// </summary>
/// <remarks>
/// The original setup path reaches this record through an upper-case text editor, then stores at
/// most ten glyphs from the original font's printable range. Online lobby display names
/// intentionally remain modern and can be longer, but the deterministic match state must use this
/// projection: it is the value the original renderer and its name-triggered rules would receive.
/// </remarks>
public static class OriginalPlayerName
{
    public const int MaximumCharacters = 10;
    private const char FirstOriginalCharacter = ' ';
    private const char LastOriginalCharacter = 'Z';

    /// <summary>
    /// Normalizes <paramref name="name"/> as the native text entry reaches the record, then bounds
    /// it to that record. Unsupported glyphs occupy their source character's cell as a space,
    /// matching the executable's setup-copy filter. The invariant per-character uppercase step
    /// turns long s (U+017F) into S, but leaves dotless i (U+0131) unsupported.
    /// </summary>
    public static string Project(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var length = Math.Min(name.Length, MaximumCharacters);
        Span<char> characters = stackalloc char[length];
        for (var index = 0; index < length; index++)
        {
            var character = char.ToUpperInvariant(name[index]);
            characters[index] = character is >= FirstOriginalCharacter
                and <= LastOriginalCharacter
                ? character
                : ' ';
        }
        return new string(characters).Trim();
    }

    /// <summary>
    /// Returns a non-empty native-record name, using a deterministic fallback when a modern
    /// display name has no representable original-font glyphs.
    /// </summary>
    public static string ProjectOrFallback(string name, string fallback)
    {
        var projected = Project(name);
        if (projected.Length != 0) return projected;

        projected = Project(fallback);
        if (projected.Length == 0)
            throw new ArgumentException("The fallback must contain an original-font glyph.", nameof(fallback));
        return projected;
    }
}
