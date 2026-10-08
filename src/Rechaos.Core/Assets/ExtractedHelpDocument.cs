using System.Text.Json.Serialization;

namespace Rechaos.Core.Assets;

public sealed record ExtractedHelpDocument(
    int FormatVersion,
    string Title,
    IReadOnlyList<ExtractedHelpTopic> Topics,
    IReadOnlyList<ExtractedHelpContentsEntry> Contents,
    IReadOnlyList<ExtractedHelpContext>? Contexts = null,
    IReadOnlyList<ExtractedHelpFont>? Fonts = null)
{
    public const int CurrentFormatVersion = 4;
}

/// <summary>
/// One help topic. <see cref="Paragraphs"/> is the only stored copy of its text; <see cref="Text"/>
/// is worked out from them each time it is read and is not serialized.
/// </summary>
public sealed record ExtractedHelpTopic(
    int Id,
    string Title,
    bool ListedInContents,
    int TopicOffset,
    IReadOnlyList<ExtractedHelpParagraph> Paragraphs)
{
    /// <summary>The topic's plain text, one line per paragraph, for searching and checks.</summary>
    [JsonIgnore]
    public string Text => string.Join('\n', Paragraphs.Select(paragraph => paragraph.Text));
}

public sealed record ExtractedHelpFont(
    string Name,
    int Family,
    int Attributes,
    int HalfPoints,
    int ForegroundRed,
    int ForegroundGreen,
    int ForegroundBlue,
    int BackgroundRed,
    int BackgroundGreen,
    int BackgroundBlue);

public enum HelpParagraphAlignment { Left, Right, Center, Unsupported }

public sealed record ExtractedHelpTabStop(int PositionUnits, int AlignmentCode);

/// <summary>
/// One paragraph of a WinHelp display record: the text up to an end-of-paragraph command, with the
/// formatting of the record it came from. A record holding several paragraphs gives each of them
/// the same formatting. A line break inside the paragraph is a newline in a run, and an empty
/// paragraph has no runs. Distances keep the source's units (FND-HELP-006).
/// </summary>
public sealed record ExtractedHelpParagraph(
    IReadOnlyList<ExtractedHelpTextRun> Runs,
    int RawFlags,
    int? SpaceBeforeUnits = null,
    int? SpaceAfterUnits = null,
    int? LineSpacingUnits = null,
    int? LeftIndentUnits = null,
    int? RightIndentUnits = null,
    int? FirstLineIndentUnits = null,
    HelpParagraphAlignment Alignment = HelpParagraphAlignment.Left,
    IReadOnlyList<ExtractedHelpTabStop>? TabStops = null,
    int? BorderFlags = null,
    int? BorderWidthUnits = null,
    bool KeepTogether = false)
{
    [JsonIgnore]
    public string Text => string.Concat(Runs.Select(run => run.Text));
}

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
    bool Popup = false,
    int? FontIndex = null);

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

/// <summary>The paragraphs of a note the rebuild adds to a help topic (DEV-HELP-002).</summary>
public static class HelpNoteParagraphs
{
    // Six points before the heading, the space the help file's body paragraphs most often take
    // (FND-HELP-006), so the note reads as a new section of the topic.
    private const int SpaceBeforeHeadingUnits = 12;
    private const int SpaceBeforeFlag = 0x0002;

    public static ExtractedHelpParagraph[] Create(string heading, string body, bool boldBody = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(heading);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        return
        [
            new ExtractedHelpParagraph([new ExtractedHelpTextRun(heading, Bold: true)],
                SpaceBeforeFlag, SpaceBeforeUnits: SpaceBeforeHeadingUnits, TabStops: []),
            new ExtractedHelpParagraph([new ExtractedHelpTextRun(body, Bold: boldBody)], 0,
                TabStops: [])
        ];
    }
}
