namespace Rechaos.Core.Assets;

public sealed record ExtractedHelpDocument(
    int FormatVersion,
    string Title,
    IReadOnlyList<ExtractedHelpTopic> Topics,
    IReadOnlyList<ExtractedHelpContentsEntry> Contents)
{
    public const int CurrentFormatVersion = 1;
}

public sealed record ExtractedHelpTopic(
    int Id,
    string Title,
    string Text,
    bool ListedInContents);

public sealed record ExtractedHelpContentsEntry(
    int Level,
    string Label,
    int? TopicId);
