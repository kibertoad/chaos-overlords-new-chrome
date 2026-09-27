using System.Buffers.Binary;
using System.Text;
using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Decodes the shipped help file and its contents file with <see cref="WinHelpDecoder"/> and
/// checks them against FMT-HELP-001 (the WinHelp container) and FMT-HELP-002 (the contents file).
/// </summary>
public sealed class OriginalHelpFileTests
{
    [Fact]
    public void HelpFileIsTheWinHelpContainerOfFmtHelp001()
    {
        var help = Assert.Single(OriginalFormatFiles.Require("FMT-HELP-001")).ReadAllBytes();
        var contents = Assert.Single(OriginalFormatFiles.Require("FMT-HELP-002")).ReadAllBytes();

        // File header.
        Assert.Equal(60_208, help.Length);
        Assert.Equal(0x00035F3Fu, U32(help, 0x00));
        var directoryOffset = I32(help, 0x04);
        Assert.Equal(0xE42, directoryOffset);
        Assert.Equal(-1, I32(help, 0x08));
        Assert.Equal((uint)help.Length, U32(help, 0x0C));

        // The directory: internal file header, B+ tree header and its one leaf page.
        Assert.Equal(4, help[directoryOffset + 8]);
        var tree = directoryOffset + 9;
        Assert.Equal(0x293B, U16(help, tree + 0x00));
        Assert.Equal(0x0402, U16(help, tree + 0x02));
        var pageSize = U16(help, tree + 0x04);
        Assert.Equal(1024, pageSize);
        Assert.Equal("z4"u8.ToArray().Concat(new byte[14]), help.AsSpan(tree + 0x06, 16).ToArray());
        Assert.Equal(0, U16(help, tree + 0x16));
        Assert.Equal(0, U16(help, tree + 0x18));
        Assert.Equal(0, U16(help, tree + 0x1A));
        Assert.Equal(-1, I16(help, tree + 0x1C));
        Assert.Equal(1, U16(help, tree + 0x1E));
        Assert.Equal(1, U16(help, tree + 0x20));
        Assert.Equal(11u, U32(help, tree + 0x22));
        var page = tree + 38;
        var unused = U16(help, page + 0x00);
        Assert.Equal(879, unused);
        Assert.Equal(11, U16(help, page + 0x02));
        Assert.Equal(-1, I16(help, page + 0x04));
        Assert.Equal(-1, I16(help, page + 0x06));

        var entries = new List<(byte[] Name, int Offset)>();
        var cursor = page + 8;
        for (var entry = 0; entry < 11; entry++)
        {
            var end = Array.IndexOf(help, (byte)0, cursor);
            entries.Add((help[cursor..end], I32(help, end + 1)));
            cursor = end + 5;
        }
        // The entries fill the page up to its unused bytes and are sorted by name in byte order.
        Assert.Equal(page + pageSize - unused, cursor);
        Assert.Equal(entries.Select(entry => entry.Name).Order(ByteOrder.Instance), entries.Select(entry => entry.Name));

        // Every internal file header, and the directory's, has used_size = reserved_size - 9, and
        // together they cover the file from 0x10 to its end without gaps.
        var internalFiles = entries.Select(entry => (entry.Offset, Flags: 0))
            .Append((Offset: directoryOffset, Flags: 4))
            .OrderBy(file => file.Offset)
            .ToArray();
        var next = 0x10;
        foreach (var (offset, flags) in internalFiles)
        {
            Assert.Equal(next, offset);
            var reserved = I32(help, offset);
            Assert.Equal(reserved - 9, I32(help, offset + 4));
            Assert.Equal(flags, help[offset + 8]);
            next = offset + reserved;
        }
        Assert.Equal(help.Length, next);

        // The rebuild's decoder reads the container, its streams and the topics without error.
        var document = WinHelpDecoder.Decode(help, contents);
        Assert.NotEmpty(document.Topics);
    }

    [Fact]
    public void ContentsFileIsTheTextOfFmtHelp002()
    {
        var help = Assert.Single(OriginalFormatFiles.Require("FMT-HELP-001")).ReadAllBytes();
        var contents = Assert.Single(OriginalFormatFiles.Require("FMT-HELP-002")).ReadAllBytes();

        // ASCII, no tabs, 75 lines each ended by CR LF, the last included.
        Assert.DoesNotContain(contents, value => value > 0x7F);
        Assert.DoesNotContain((byte)'\t', contents);
        var text = Encoding.ASCII.GetString(contents);
        Assert.EndsWith("\r\n", text);
        var lines = text[..^2].Split("\r\n");
        Assert.Equal(75, lines.Length);
        Assert.All(lines, line => Assert.DoesNotContain('\r', line));
        Assert.All(lines, line => Assert.DoesNotContain('\n', line));

        Assert.Equal(":Base Chaos.hlp>main", lines[0]);
        Assert.StartsWith(":Title ", lines[1]);
        var headings = 0;
        var topics = 0;
        foreach (var line in lines.Skip(2))
        {
            if (line.StartsWith("1 ", StringComparison.Ordinal) && !line.Contains('='))
                headings++;
            else if (line.StartsWith("2 ", StringComparison.Ordinal) && line.Contains('=') && headings > 0)
                topics++;
            else
                Assert.Fail($"'{line}' is none of the line kinds of FMT-HELP-002");
        }
        Assert.Equal(14, headings);
        Assert.Equal(59, topics);

        // The rebuild's decoder: one entry per heading and topic line, in order, with the title
        // from the second line. It refuses a context name missing from the help file's context
        // tree, so a successful decode checks every one.
        var document = WinHelpDecoder.Decode(help, contents);
        Assert.Equal(lines[1][":Title ".Length..], document.Title);
        Assert.Equal(73, document.Contents.Count);
        foreach (var (entry, line) in document.Contents.Zip(lines.Skip(2)))
        {
            var body = line[2..];
            var separator = body.IndexOf('=');
            if (line[0] == '1')
            {
                Assert.Equal(0, entry.Level);
                Assert.Equal(body, entry.Label);
                Assert.Null(entry.ContextName);
                Assert.Null(entry.TopicId);
            }
            else
            {
                Assert.Equal(1, entry.Level);
                Assert.Equal(body[..separator], entry.Label);
                Assert.Equal(body[(separator + 1)..], entry.ContextName);
                Assert.NotNull(entry.TopicId);
            }
        }
    }

    private sealed class ByteOrder : IComparer<byte[]>
    {
        public static readonly ByteOrder Instance = new();
        public int Compare(byte[]? left, byte[]? right) => left.AsSpan().SequenceCompareTo(right);
    }

    private static int U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static short I16(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static int I32(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
}
