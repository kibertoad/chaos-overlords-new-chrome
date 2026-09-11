namespace Rechaos.Core.Assets;

public sealed record ExtractedHelpDocument(
    int FormatVersion,
    string Title,
    IReadOnlyList<ExtractedHelpTopic> Topics,
    IReadOnlyList<ExtractedHelpContentsEntry> Contents,
    IReadOnlyList<ExtractedHelpContext>? Contexts = null)
{
    public const int CurrentFormatVersion = 3;
}

public sealed record ExtractedHelpTopic(
    int Id,
    string Title,
    string Text,
    bool ListedInContents,
    int TopicOffset = 0,
    IReadOnlyList<ExtractedHelpTextRun>? Runs = null);

public sealed record ExtractedHelpTextRun(
    string Text,
    bool Bold = false,
    bool Italic = false,
    bool Underline = false,
    bool Strikethrough = false,
    bool DoubleUnderline = false,
    bool SmallCaps = false,
    int HalfPoints = 20,
    uint? LinkHash = null,
    bool Popup = false);

public sealed record ExtractedHelpContext(
    string? Name,
    uint? Hash,
    uint? NumericId,
    int TargetOffset);

public sealed record ExtractedHelpContentsEntry(
    int Level,
    string Label,
    int? TopicId,
    string? ContextName = null);
