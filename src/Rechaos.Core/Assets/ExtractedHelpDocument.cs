namespace Rechaos.Core.Assets;

public sealed record ExtractedHelpDocument(
    int FormatVersion,
    string Title,
    IReadOnlyList<ExtractedHelpTopic> Topics,
    IReadOnlyList<ExtractedHelpContentsEntry> Contents,
    IReadOnlyList<ExtractedHelpContext>? Contexts = null)
{
    public const int CurrentFormatVersion = 2;
}

public sealed record ExtractedHelpTopic(
    int Id,
    string Title,
    string Text,
    bool ListedInContents);

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
