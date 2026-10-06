using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

/// <summary>
/// How the lobby chat is laid out as lines of the original font.
/// </summary>
/// <remarks>
/// The font has fixed-width cells, so a panel is a number of columns and rows. Each message is
/// drawn as its author's name, a colon and the text, wrapped at spaces where a word fits and cut
/// where one does not. The panel shows the newest rows, so the conversation reads downwards and
/// scrolls up as it grows.
/// </remarks>
internal static class LobbyChatPresentation
{
    /// <summary>How much of an author's name a message is labelled with.</summary>
    public const int NameColumns = 10;

    /// <summary>The longest message the server takes, which bounds the input.</summary>
    public const int MessageColumns = 160;

    /// <summary>How many messages the lobby keeps to draw from. Older ones are dropped.</summary>
    public const int KeptMessages = 200;

    /// <summary>
    /// Whether the game's own chat input takes a character: only one the original font can draw.
    /// </summary>
    /// <remarks>
    /// The server accepts any safe text, so a message from another client may still hold characters
    /// this font has no glyph for. Those are drawn as blanks.
    /// </remarks>
    public static bool Accepts(char character) =>
        character == '\b' || (character >= ' ' && OriginalFontLayout.TryGlyph(character, out _));

    /// <summary>
    /// The last <paramref name="rows"/> rows of the conversation, wrapped to
    /// <paramref name="columns"/> characters.
    /// </summary>
    /// <param name="messages">The messages, oldest first.</param>
    /// <param name="authorName">The name to label a message with, by player id.</param>
    public static IReadOnlyList<string> Rows(
        IReadOnlyList<LobbyChatLine> messages,
        Func<string, string> authorName,
        int columns,
        int rows)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(authorName);
        if (columns <= 0 || rows <= 0) return [];
        var wrapped = new List<string>();
        foreach (var message in messages)
        {
            var name = authorName(message.PlayerId).Trim();
            if (name.Length > NameColumns) name = name[..NameColumns];
            Wrap($"{name}: {message.Text}", columns, wrapped);
        }
        return wrapped.Count <= rows ? wrapped : wrapped.GetRange(wrapped.Count - rows, rows);
    }

    /// <summary>Breaks one line into rows of at most <paramref name="columns"/> characters.</summary>
    internal static void Wrap(string text, int columns, List<string> into)
    {
        var rest = text.Trim();
        while (rest.Length > columns)
        {
            var space = rest.LastIndexOf(' ', columns);
            var cut = space > 0 ? space : columns;
            into.Add(rest[..cut].TrimEnd());
            rest = rest[cut..].TrimStart();
        }
        if (rest.Length > 0) into.Add(rest);
    }
}
