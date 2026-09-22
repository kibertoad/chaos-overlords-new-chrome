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
    private const char LatinSmallLongS = '\u017F';

    /// <summary>
    /// Normalizes <paramref name="name"/> as the native text entry reaches the record, then bounds
    /// it to that record. Unsupported glyphs occupy their source character's cell as a space,
    /// matching the executable's setup-copy filter.
    /// </summary>
    /// <remarks>
    /// Each seat's projection feeds the deterministic match state, so it must not depend on the
    /// runtime's globalization backend. Rather than asking <see cref="char.ToUpperInvariant"/>
    /// (whose tables differ between ICU versions and NLS), the fold names exactly the cells that
    /// Unicode's simple upper-case mapping sends into the record's range: <c>a</c>–<c>z</c> and
    /// the long s. Every other cell either already sits in that range or becomes a space. The
    /// server's reserved-name check mirrors this list in <c>originalPlayerNameProjection</c>.
    /// </remarks>
    public static string Project(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var length = Math.Min(name.Length, MaximumCharacters);
        Span<char> characters = stackalloc char[length];
        for (var index = 0; index < length; index++)
        {
            characters[index] = FoldIntoRecord(name[index]);
        }
        return new string(characters).Trim();
    }

    private static char FoldIntoRecord(char character) => character switch
    {
        >= 'a' and <= 'z' => (char)(character - ('a' - 'A')),
        LatinSmallLongS => 'S',
        >= FirstOriginalCharacter and <= LastOriginalCharacter => character,
        _ => ' '
    };

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
