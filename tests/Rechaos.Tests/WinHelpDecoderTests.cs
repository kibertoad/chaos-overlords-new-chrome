using System.Buffers.Binary;
using System.Text;
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
        var entry = Assert.Single(document.Contents);
        Assert.Equal(0, entry.Level);
        Assert.Equal("Synthetic", entry.Label);
        Assert.Equal(topic.Id, entry.TopicId);
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

        var text = Assert.Single(document.Topics).Text;
        Assert.Contains("Attack Roll = gang Combat + current Force - defender Defense", text,
            StringComparison.Ordinal);
        Assert.Contains("gang eliminated during the round still attacks", text,
            StringComparison.Ordinal);
    }

    private static byte[] BuildHelpFile(string topicName = "Synthetic")
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
        byte[] paragraphCommands =
        [
            0, 0x80, 22, 0, 0, 0, 0, 0, 0,
            0x81, 0xff
        ];
        var topicText = Encoding.ASCII.GetBytes("Hello\0world\0");
        var display = BuildTopicLink(0x20, paragraphCommands, topicText, uint.MaxValue);
        var uncompressedTopic = topicHeader.Concat(display).ToArray();
        var compressedTopic = LiteralCompress(uncompressedTopic);
        var topicStream = new byte[12 + compressedTopic.Length];
        WriteInt32(topicStream, 4, 12);
        compressedTopic.CopyTo(topicStream, 12);

        var streams = new Dictionary<string, byte[]>
        {
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
