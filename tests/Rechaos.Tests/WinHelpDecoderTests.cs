using System.Buffers.Binary;
using System.Text;
using Rechaos.Core.Assets;
using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

public sealed class WinHelpDecoderTests
{
    [Fact]
    public void Lz77DecoderHandlesLiteralsAndOverlappingBackReferences()
    {
        Assert.Equal("ABCDEFGH", Encoding.ASCII.GetString(
            WinHelpDecoder.DecompressLz77(
                [0, (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'G', (byte)'H'],
                32)));
        Assert.Equal("AAAA", Encoding.ASCII.GetString(
            WinHelpDecoder.DecompressLz77([2, (byte)'A', 0, 0], 32)));
        Assert.Throws<InvalidDataException>(() =>
            WinHelpDecoder.DecompressLz77([2, (byte)'A', 0, 0], 3));
    }

    [Fact]
    public void HallDecoderHandlesPhrasesLiteralsSpacesAndTerminators()
    {
        var decoded = WinHelpDecoder.DecompressHall(
            [0, 2, 3, (byte)'X', 7, 15],
            [Encoding.ASCII.GetBytes("THE"), Encoding.ASCII.GetBytes("GAME")],
            32);

        Assert.Equal([.. Encoding.ASCII.GetBytes("THEGAMEX "), (byte)0], decoded);
        Assert.Throws<InvalidDataException>(() =>
            WinHelpDecoder.DecompressHall([4], [Encoding.ASCII.GetBytes("ONLY")], 32));
    }

    [Fact]
    public void SyntheticWinHelpContainerProducesStructuredModernHelp()
    {
        var help = BuildHelpFile();
        var contents = Encoding.ASCII.GetBytes(":Title Synthetic Help\r\n1 Synthetic=SYNTH\r\n");

        var document = WinHelpDecoder.Decode(help, contents);

        Assert.Equal("Synthetic Help", document.Title);
        var topic = Assert.Single(document.Topics);
        Assert.Equal("Synthetic", topic.Title);
        Assert.Equal("Hello\nworld", topic.Text);
        Assert.True(topic.ListedInContents);
        Assert.Equal(0, topic.TopicOffset);
        Assert.Equal(2, document.Fonts!.Count);
        Assert.Equal("Times New Roman", document.Fonts[0].Name);
        Assert.Equal(20, document.Fonts[0].HalfPoints);
        // A line break (0x81) stays inside the paragraph.
        Assert.Equal("Hello\nworld", Assert.Single(topic.Paragraphs).Text);
        Assert.Collection(document.Contexts!,
            context =>
            {
                Assert.Equal("SYNTH", context.Name);
                Assert.Equal(WinHelpDecoder.CalculateContextHash("SYNTH"), context.Hash);
                Assert.Null(context.NumericId);
                Assert.Equal(0, context.TargetOffset);
            },
            context =>
            {
                Assert.Null(context.Name);
                Assert.Null(context.Hash);
                Assert.Equal(7001u, context.NumericId);
                Assert.Equal(0, context.TargetOffset);
            });
        var entry = Assert.Single(document.Contents);
        Assert.Equal(0, entry.Level);
        Assert.Equal("Synthetic", entry.Label);
        Assert.Equal(topic.Id, entry.TopicId);
        Assert.Equal("SYNTH", entry.ContextName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecoderPreservesLegacyFontStylesAndInternalTopicLinks(bool popup)
    {
        var document = WinHelpDecoder.Decode(
            BuildHelpFile(styledLink: true, popupLink: popup),
            Encoding.ASCII.GetBytes(":Title Synthetic Help\r\n1 Synthetic=SYNTH\r\n"));

        var topic = Assert.Single(document.Topics);
        Assert.Equal("Plain Bold City", topic.Text);
        var runs = Assert.Single(topic.Paragraphs).Runs;
        Assert.Contains(runs, run => run.Text.Contains("Bold", StringComparison.Ordinal)
                                     && run.Bold && run.Italic);
        var link = Assert.Single(runs, run => run.LinkHash is not null);
        Assert.Equal("City", link.Text);
        Assert.Equal(WinHelpDecoder.CalculateContextHash("CITYVIEW"), link.LinkHash);
        Assert.Equal(popup, link.Popup);
        Assert.Equal(20, link.HalfPoints);
        Assert.Equal(1, Assert.Single(runs, run => run.Text.Contains("Bold", StringComparison.Ordinal)).FontIndex);
    }

    // The paragraph fields of a display record (FND-HELP-006).
    [Fact]
    public void DecoderPreservesParagraphGeometryFromDisplayRecord()
    {
        var document = WinHelpDecoder.Decode(BuildHelpFile(geometry: true),
            ReadOnlyMemory<byte>.Empty);
        var paragraph = Assert.Single(Assert.Single(document.Topics).Paragraphs);

        Assert.Equal(0x087e, paragraph.RawFlags);
        Assert.Equal(12, paragraph.SpaceBeforeUnits);
        Assert.Equal(6, paragraph.SpaceAfterUnits);
        Assert.Equal(20, paragraph.LineSpacingUnits);
        Assert.Equal(18, paragraph.LeftIndentUnits);
        Assert.Equal(12, paragraph.RightIndentUnits);
        Assert.Equal(-6, paragraph.FirstLineIndentUnits);
        Assert.Equal(HelpParagraphAlignment.Center, paragraph.Alignment);
    }

    // An end-of-paragraph command (0x82) inside a display record starts a new paragraph that
    // keeps the record's formatting (FND-HELP-006).
    [Fact]
    public void EndOfParagraphSplitsDisplayRecordIntoParagraphsWithItsFormatting()
    {
        var document = WinHelpDecoder.Decode(
            BuildHelpFile(geometry: true, paragraphBreak: true), ReadOnlyMemory<byte>.Empty);
        var paragraphs = Assert.Single(document.Topics).Paragraphs;

        Assert.Equal(["Hello", "world"], paragraphs.Select(paragraph => paragraph.Text));
        Assert.All(paragraphs, paragraph =>
        {
            Assert.Equal(12, paragraph.SpaceBeforeUnits);
            Assert.Equal(6, paragraph.SpaceAfterUnits);
            Assert.Equal(HelpParagraphAlignment.Center, paragraph.Alignment);
        });
    }

    // A font descriptor naming a face past the face table takes face 0 and is reported, so one
    // damaged descriptor does not cost the whole help file (DEV-HELP-001).
    [Fact]
    public void FontDescriptorWithFaceIndexPastTheTableUsesTheFirstFace()
    {
        var warnings = new List<string>();

        var document = WinHelpDecoder.Decode(BuildHelpFile(badFaceIndex: true),
            Encoding.ASCII.GetBytes(":Title Synthetic Help\r\n1 Synthetic=SYNTH\r\n"),
            warnings.Add);

        Assert.Equal(["Times New Roman", "Times New Roman"],
            document.Fonts!.Select(font => font.Name));
        var warning = Assert.Single(warnings);
        Assert.Contains("descriptor 1", warning, StringComparison.Ordinal);
        Assert.Contains("face 5", warning, StringComparison.Ordinal);
        Assert.Equal("Hello\nworld", Assert.Single(document.Topics).Text);
    }

    [Theory]
    [InlineData("", 0x00000001u)]
    [InlineData("INTRO", 0x053d9a5cu)]
    [InlineData("CITYVIEW", 0x86ee9810u)]
    [InlineData("ITEMINFO", 0xeb824cedu)]
    public void ContextHashMatchesWinHelpContextTreeKeys(string name, uint expected) =>
        Assert.Equal(expected, WinHelpDecoder.CalculateContextHash(name));

    [Fact]
    public void DecoderRejectsContentsReferencesMissingFromNativeContextTree()
    {
        var contents = Encoding.ASCII.GetBytes(":Title Synthetic Help\r\n1 Missing=MISSING\r\n");

        var error = Assert.Throws<InvalidDataException>(() =>
            WinHelpDecoder.Decode(BuildHelpFile(), contents));

        Assert.Contains("MISSING", error.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => WinHelpDecoder.CalculateContextHash("NOT VALID"));
    }

    [Fact]
    public void DecoderRejectsInvalidOrTruncatedContainers()
    {
        Assert.Throws<InvalidDataException>(() =>
            WinHelpDecoder.Decode(new byte[16], ReadOnlyMemory<byte>.Empty));
        var valid = BuildHelpFile();
        Assert.Throws<InvalidDataException>(() =>
            WinHelpDecoder.Decode(valid.AsMemory(0, valid.Length - 1), ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public void AttackTopicDisplaysCorrectedForceInclusiveSimultaneousRule()
    {
        var document = WinHelpDecoder.Decode(
            BuildHelpFile("Attack..."), ReadOnlyMemory<byte>.Empty);

        var topic = Assert.Single(document.Topics);
        var text = topic.Text;
        Assert.Contains(topic.Paragraphs, paragraph =>
            paragraph.Text == "NEW CHROME CLARIFICATION" && paragraph.Runs.All(run => run.Bold));
        Assert.Contains("Attack Roll = gang Combat + current Force - defender Defense", text,
            StringComparison.Ordinal);
        Assert.Contains("gang eliminated during the round still attacks", text,
            StringComparison.Ordinal);
    }

    private static byte[] BuildHelpFile(
        string topicName = "Synthetic",
        bool styledLink = false,
        bool popupLink = false,
        bool geometry = false,
        bool paragraphBreak = false,
        bool badFaceIndex = false)
    {
        var system = new byte[12];
        WriteUInt16(system, 2, 33);
        WriteUInt16(system, 10, 4);
        var phraseIndex = new byte[28];
        var topicHeaderData = new byte[28];
        var topicTitle = Encoding.ASCII.GetBytes(topicName + "\0");
        var topicHeaderLength = 21 + topicHeaderData.Length + topicTitle.Length;
        var topicHeader = BuildTopicLink(0x02, topicHeaderData, topicTitle,
            checked((uint)(12 + topicHeaderLength)));
        var paragraphCommands = geometry
            ? new byte[] { 0, 0x80, 22, 0, 0, 0, 0, 0x7e, 0x08,
                152, 140, 168, 164, 152, 116, paragraphBreak ? (byte)0x82 : (byte)0x81,
                0x82, 0xff }
            : styledLink
            ? StyledParagraphCommands(popupLink)
            : new byte[] { 0, 0x80, 22, 0, 0, 0, 0, 0, 0, 0x81, 0xff };
        var topicText = Encoding.ASCII.GetBytes(styledLink
            ? "Plain \0Bold \0City\0"
            : "Hello\0world\0");
        var display = BuildTopicLink(0x20, paragraphCommands, topicText, uint.MaxValue);
        var uncompressedTopic = topicHeader.Concat(display).ToArray();
        var compressedTopic = LiteralCompress(uncompressedTopic);
        var topicStream = new byte[12 + compressedTopic.Length];
        WriteInt32(topicStream, 4, 12);
        compressedTopic.CopyTo(topicStream, 12);

        var streams = new Dictionary<string, byte[]>
        {
            ["|CONTEXT"] = styledLink
                ? BuildContextTree(("SYNTH", 0), ("CITYVIEW", 0))
                : BuildContextTree(("SYNTH", 0)),
            ["|CTXOMAP"] = BuildContextIdMap(7001, 0),
            ["|FONT"] = BuildFontTable(badFaceIndex),
            ["|PhrImage"] = [],
            ["|PhrIndex"] = phraseIndex,
            ["|SYSTEM"] = system,
            ["|TOPIC"] = topicStream
        };
        var output = new List<byte>(new byte[16]);
        var offsets = new Dictionary<string, int>();
        foreach (var stream in streams)
        {
            offsets[stream.Key] = output.Count;
            var header = new byte[9];
            WriteInt32(header, 0, stream.Value.Length + 9);
            WriteInt32(header, 4, stream.Value.Length);
            output.AddRange(header);
            output.AddRange(stream.Value);
        }

        var directoryOffset = output.Count;
        var tree = new byte[38 + 1024];
        WriteUInt16(tree, 0, 0x293b);
        WriteUInt16(tree, 4, 1024);
        WriteInt16(tree, 26, 0);
        WriteInt16(tree, 28, -1);
        WriteInt16(tree, 30, 1);
        WriteInt16(tree, 32, 1);
        WriteInt32(tree, 34, offsets.Count);
        var pageOffset = 38;
        WriteInt16(tree, pageOffset + 2, checked((short)offsets.Count));
        WriteInt16(tree, pageOffset + 4, -1);
        WriteInt16(tree, pageOffset + 6, -1);
        var entryOffset = pageOffset + 8;
        foreach (var stream in offsets)
        {
            var name = Encoding.ASCII.GetBytes(stream.Key);
            name.CopyTo(tree, entryOffset);
            entryOffset += name.Length + 1;
            WriteInt32(tree, entryOffset, stream.Value);
            entryOffset += 4;
        }
        var directoryHeader = new byte[9];
        WriteInt32(directoryHeader, 0, tree.Length + 9);
        WriteInt32(directoryHeader, 4, tree.Length);
        output.AddRange(directoryHeader);
        output.AddRange(tree);

        var result = output.ToArray();
        WriteUInt32(result, 0, 0x00035f3f);
        WriteInt32(result, 4, directoryOffset);
        WriteInt32(result, 8, -1);
        WriteInt32(result, 12, result.Length);
        return result;
    }

    private static byte[] StyledParagraphCommands(bool popup)
    {
        var result = new byte[20];
        result[0] = 0;
        result[1] = 0x80;
        result[2] = 30;
        result[9] = 0x80;
        WriteInt16(result, 10, 1);
        result[12] = popup ? (byte)0xe2 : (byte)0xe3;
        WriteUInt32(result, 13, WinHelpDecoder.CalculateContextHash("CITYVIEW"));
        result[17] = 0x89;
        result[18] = 0xff;
        return result;
    }

    private static byte[] BuildFontTable(bool badFaceIndex = false)
    {
        var result = new byte[30 + 22];
        WriteUInt16(result, 0, 1);
        WriteUInt16(result, 2, 2);
        WriteUInt16(result, 4, 8);
        WriteUInt16(result, 6, 30);
        Encoding.ASCII.GetBytes("Times New Roman").CopyTo(result, 8);
        result[30 + 1] = 20;
        result[41] = 0x03;
        result[41 + 1] = 20;
        if (badFaceIndex) WriteUInt16(result, 41 + 3, 5);
        return result;
    }

    private static byte[] BuildContextTree(params (string Name, int TopicOffset)[] entries)
    {
        var tree = new byte[38 + 1024];
        WriteUInt16(tree, 0, 0x293b);
        WriteUInt16(tree, 4, 1024);
        WriteInt16(tree, 26, 0);
        WriteInt16(tree, 28, -1);
        WriteInt16(tree, 30, 1);
        WriteInt16(tree, 32, 1);
        WriteInt32(tree, 34, entries.Length);
        WriteInt16(tree, 38 + 2, checked((short)entries.Length));
        WriteInt16(tree, 38 + 4, -1);
        WriteInt16(tree, 38 + 6, -1);
        for (var index = 0; index < entries.Length; index++)
        {
            WriteUInt32(tree, 38 + 8 + index * 8,
                WinHelpDecoder.CalculateContextHash(entries[index].Name));
            WriteInt32(tree, 38 + 12 + index * 8, entries[index].TopicOffset);
        }
        return tree;
    }

    private static byte[] BuildContextIdMap(uint contextId, int topicOffset)
    {
        var result = new byte[10];
        WriteUInt16(result, 0, 1);
        WriteUInt32(result, 2, contextId);
        WriteInt32(result, 6, topicOffset);
        return result;
    }

    private static byte[] BuildTopicLink(
        byte recordType,
        byte[] data1,
        byte[] data2,
        uint nextBlock)
    {
        var result = new byte[21 + data1.Length + data2.Length];
        WriteUInt32(result, 0, checked((uint)result.Length));
        WriteUInt32(result, 4, checked((uint)data2.Length));
        WriteUInt32(result, 8, uint.MaxValue);
        WriteUInt32(result, 12, nextBlock);
        WriteUInt32(result, 16, checked((uint)(21 + data1.Length)));
        result[20] = recordType;
        data1.CopyTo(result, 21);
        data2.CopyTo(result, 21 + data1.Length);
        return result;
    }

    private static byte[] LiteralCompress(byte[] input)
    {
        var output = new List<byte>();
        for (var offset = 0; offset < input.Length; offset += 8)
        {
            output.Add(0);
            output.AddRange(input.Skip(offset).Take(Math.Min(8, input.Length - offset)));
        }
        return output.ToArray();
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteInt16(byte[] bytes, int offset, short value) =>
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static void WriteInt32(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
}
