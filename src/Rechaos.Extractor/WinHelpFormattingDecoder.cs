using System.Text;
using Rechaos.Core.Assets;

namespace Rechaos.Extractor;

public static partial class WinHelpDecoder
{
    private static IReadOnlyList<HelpFont> ReadFonts(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8) throw new InvalidDataException("WinHelp font table is truncated.");
        var faceNames = ReadUInt16(data);
        var descriptors = ReadUInt16(data, 2);
        var faceNamesOffset = ReadUInt16(data, 4);
        var descriptorsOffset = ReadUInt16(data, 6);
        if (faceNames is > 256 || descriptors is > 256
            || faceNamesOffset < 8 || descriptorsOffset < faceNamesOffset
            || faceNamesOffset >= 12)
            throw new InvalidDataException("WinHelp font table layout is unsupported.");
        Require(data, descriptorsOffset, checked(descriptors * 11));
        var result = new HelpFont[descriptors];
        for (var index = 0; index < descriptors; index++)
        {
            var offset = descriptorsOffset + index * 11;
            var attributes = data[offset];
            var halfPoints = data[offset + 1];
            if ((attributes & 0xc0) != 0 || halfPoints is 0 or > 144)
                throw new InvalidDataException("WinHelp font descriptor is invalid.");
            result[index] = new HelpFont(attributes, halfPoints);
        }
        return result;
    }

    private static HelpFont FontAt(IReadOnlyList<HelpFont> fonts, int index) =>
        index >= 0 && index < fonts.Count ? fonts[index] : HelpFont.Default;

    private static void AddRun(
        List<MutableRun> runs,
        string text,
        HelpFont font,
        uint? linkHash,
        bool popup)
    {
        if (text.Length == 0) return;
        if (runs.LastOrDefault() is { } previous
            && previous.Font == font
            && previous.LinkHash == linkHash
            && previous.Popup == popup)
        {
            previous.Text.Append(text);
            return;
        }
        runs.Add(new MutableRun(text, font, linkHash, popup));
    }

    private static IReadOnlyList<ExtractedHelpTextRun> NormalizeRuns(
        IReadOnlyList<MutableRun> source)
    {
        var rawLines = new List<List<StyledCharacter>> { new() };
        foreach (var run in source)
        foreach (var character in run.Text.ToString())
        {
            if (character == '\r') continue;
            if (character == '\n') rawLines.Add([]);
            else rawLines[^1].Add(new StyledCharacter(character, run.Font,
                run.LinkHash, run.Popup));
        }

        var lines = new List<List<StyledCharacter>>(rawLines.Count);
        foreach (var rawLine in rawLines)
        {
            var line = new List<StyledCharacter>(rawLine.Count);
            var pendingSpace = false;
            StyledCharacter space = default;
            foreach (var character in rawLine)
            {
                if (character.Value is ' ' or '\t')
                {
                    if (line.Count > 0)
                    {
                        pendingSpace = true;
                        space = character with { Value = ' ' };
                    }
                    continue;
                }
                if (pendingSpace) line.Add(space);
                pendingSpace = false;
                line.Add(character);
            }
            if (line.Count == 0 && (lines.Count == 0 || lines[^1].Count == 0)) continue;
            lines.Add(line);
        }
        while (lines.Count > 0 && lines[^1].Count == 0) lines.RemoveAt(lines.Count - 1);

        var result = new List<ExtractedHelpTextRun>();
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            foreach (var character in lines[lineIndex]) AddNormalizedCharacter(result, character);
            if (lineIndex + 1 < lines.Count)
                AddNormalizedCharacter(result, new StyledCharacter('\n', HelpFont.Default, null, false));
        }
        return result;
    }

    private static void AddNormalizedCharacter(
        List<ExtractedHelpTextRun> runs,
        StyledCharacter character)
    {
        var font = character.Font;
        var run = new ExtractedHelpTextRun(
            character.Value.ToString(),
            (font.Attributes & 0x01) != 0,
            (font.Attributes & 0x02) != 0,
            (font.Attributes & 0x04) != 0,
            (font.Attributes & 0x08) != 0,
            (font.Attributes & 0x10) != 0,
            (font.Attributes & 0x20) != 0,
            font.HalfPoints,
            character.LinkHash,
            character.Popup);
        if (runs.LastOrDefault() is { } previous && SameFormatting(previous, run))
            runs[^1] = previous with { Text = previous.Text + run.Text };
        else
            runs.Add(run);
    }

    private static bool SameFormatting(ExtractedHelpTextRun left, ExtractedHelpTextRun right) =>
        left.Bold == right.Bold
        && left.Italic == right.Italic
        && left.Underline == right.Underline
        && left.Strikethrough == right.Strikethrough
        && left.DoubleUnderline == right.DoubleUnderline
        && left.SmallCaps == right.SmallCaps
        && left.HalfPoints == right.HalfPoints
        && left.LinkHash == right.LinkHash
        && left.Popup == right.Popup;

    private readonly record struct HelpFont(byte Attributes, byte HalfPoints)
    {
        public static HelpFont Default => new(0, 20);
    }

    private readonly record struct StyledCharacter(
        char Value,
        HelpFont Font,
        uint? LinkHash,
        bool Popup);
}
