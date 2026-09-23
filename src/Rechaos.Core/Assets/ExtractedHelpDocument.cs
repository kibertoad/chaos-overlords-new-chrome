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

public sealed record ExtractedHelpTopic(
    int Id,
    string Title,
    string Text,
    bool ListedInContents,
    int TopicOffset = 0,
    IReadOnlyList<ExtractedHelpTextRun>? Runs = null,
    IReadOnlyList<ExtractedHelpParagraph>? Paragraphs = null);

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

/// <summary>One WinHelp display record. Distances retain the source's layout units.</summary>
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
    bool KeepTogether = false);

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
