using System.Buffers.Binary;
using System.Text;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Checks every shipped sound effect against FMT-AUDIO-001 and every music track against
/// FMT-AUDIO-002. The rebuild has no decoder of its own for either: the extractor copies the
/// files unchanged and MonoGame plays them. These tests read the files by the entries' tables so
/// the bytes handed to MonoGame are known to be the plain PCM WAVE and Ogg Vorbis files the
/// entries describe.
/// </summary>
public sealed class OriginalSoundFileTests
{
    [Fact]
    public void SoundEffectsAreTheTwoChunkWaveFilesOfFmtAudio001()
    {
        var files = OriginalFormatFiles.Require("FMT-AUDIO-001");
        // FMT-AUDIO-001: the 28 sound files.
        Assert.Equal(28, files.Count);
        var odd = new List<string>();
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var bytes = file.ReadAllBytes();
            Text(bytes, 0x00, "RIFF");
            Text(bytes, 0x08, "WAVE");
            Text(bytes, 0x0C, "fmt ");
            Equal("fmt_size", 16, U32(bytes, 0x10));
            Equal("format_tag", 1, U16(bytes, 0x14));
            Equal("channels", 1, U16(bytes, 0x16));
            Equal("sample_rate", 22_050, U32(bytes, 0x18));
            Equal("byte_rate", 22_050, U32(bytes, 0x1C));
            Equal("block_align", 1, U16(bytes, 0x20));
            Equal("bits_per_sample", 8, U16(bytes, 0x22));
            Text(bytes, 0x24, "data");
            var dataSize = U32(bytes, 0x28);
            Equal("riff_size", 36 + dataSize, U32(bytes, 0x04));
            // The file ends right after the samples and, for an odd data_size, the pad byte.
            Equal("file size", 44 + dataSize + dataSize % 2, bytes.Length);
            if (dataSize % 2 == 1) odd.Add(file.Name);
        });
        // FMT-AUDIO-001, pad: the six files with an odd data_size.
        Assert.Equal(
            ["SND00200", "SND00201", "SND00204", "SND00502", "SND00503", "SND00507"],
            odd.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void MusicTracksAreTheOggPagesOfFmtAudio002()
    {
        var files = OriginalFormatFiles.Require("FMT-AUDIO-002");
        // FMT-AUDIO-002: the eight tracks, which are the files the rebuild's soundtrack plays.
        Assert.Equal(
            SoundtrackCatalog.ExpectedFileNames.Order(StringComparer.Ordinal),
            files.Select(file => file.Name.ToLowerInvariant()).Order(StringComparer.Ordinal));
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var bytes = file.ReadAllBytes();
            var offset = 0;
            uint sequence = 0;
            uint? serial = null;
            while (offset < bytes.Length)
            {
                var page = bytes.AsSpan(offset);
                if (page.Length < 27) throw new InvalidDataException($"a page at {offset} is truncated");
                Text(bytes, offset, "OggS");
                Equal("version", 0, page[4]);
                var headerType = page[5];
                if ((headerType & 0xF8) != 0) throw new InvalidDataException($"page {sequence} sets unused flag bits");
                var segmentCount = page[26];
                var bodySize = 0;
                for (var index = 0; index < segmentCount; index++) bodySize += page[27 + index];
                var pageSize = 27 + segmentCount + bodySize;
                if (pageSize > page.Length) throw new InvalidDataException($"page {sequence} runs past the file");
                var pageSerial = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(14, 4));
                serial ??= pageSerial;
                Equal("serial", serial.Value, pageSerial);
                Equal("sequence", sequence, BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(18, 4)));
                Equal($"checksum of page {sequence}", BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(22, 4)),
                    OggCrc(page[..pageSize]));
                var first = offset == 0;
                var last = offset + pageSize == bytes.Length;
                Equal($"first_page of page {sequence}", first ? 1 : 0, (headerType >> 1) & 1);
                Equal($"last_page of page {sequence}", last ? 1 : 0, (headerType >> 2) & 1);
                if (first)
                {
                    // The first page's body is the Vorbis identification header: two channels at
                    // 44,100 samples per second.
                    var body = page.Slice(27 + segmentCount, bodySize);
                    if (body.Length < 16 || body[0] != 1 || !body.Slice(1, 6).SequenceEqual("vorbis"u8))
                        throw new InvalidDataException("the first page does not hold the Vorbis identification header");
                    Equal("Vorbis channels", 2, body[11]);
                    Equal("Vorbis sample rate", 44_100, BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(12, 4)));
                }
                offset += pageSize;
                sequence++;
            }
            // The pages cover the file from its first byte to its last.
            Equal("bytes covered by pages", bytes.Length, offset);
        });
    }

    /// <summary>The CRC-32 of an Ogg page as RFC 3533 defines it, with the checksum field zeroed.</summary>
    private static uint OggCrc(ReadOnlySpan<byte> page)
    {
        uint crc = 0;
        for (var index = 0; index < page.Length; index++)
        {
            var value = index is >= 22 and < 26 ? (byte)0 : page[index];
            crc = (crc << 8) ^ CrcTable[(int)((crc >> 24) ^ value)];
        }
        return crc;
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(entry =>
    {
        var crc = (uint)entry << 24;
        for (var bit = 0; bit < 8; bit++)
            crc = (crc & 0x8000_0000) != 0 ? (crc << 1) ^ 0x04C1_1DB7 : crc << 1;
        return crc;
    }).ToArray();

    private static void Text(byte[] bytes, int offset, string expected)
    {
        var actual = Encoding.ASCII.GetString(bytes, offset, expected.Length);
        if (actual != expected) throw new InvalidDataException($"'{expected}' expected at {offset}, found '{actual}'");
    }

    private static long U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static int U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));

    private static void Equal(string label, long expected, long actual)
    {
        if (expected != actual) throw new InvalidDataException($"{label} is {actual}, expected {expected}");
    }
}
