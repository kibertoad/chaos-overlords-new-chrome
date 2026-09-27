using System.Buffers.Binary;
using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Decodes every shipped PX image the way the extractor does and checks it against FMT-GFX-001
/// (the 16-bit files), FMT-GFX-002 (the 8-bit files) and FMT-GFX-003 (their palette entries).
/// The files carry no width or height; the rebuild takes them from <see cref="PxDimensions"/>,
/// which is compared with the Coverage table of FMT-GFX-001.
/// </summary>
public sealed class OriginalImageFileTests
{
    [Fact]
    public void Px16FilesHaveTheFmtGfx001HeaderAndTheDocumentedSizes()
    {
        var files = OriginalFormatFiles.Require("FMT-GFX-001");
        // FMT-GFX-001, Coverage: all 215 files under DATA/PX16/.
        Assert.Equal(215, files.Count);
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var bytes = file.ReadAllBytes();
            var header = new BitmapHeader(bytes);
            header.Expect(pixelOffset: 54, bitCount: 16, compression: 0, pixelsPerMeter: 0);
            Equal("image_size", bytes.Length - 54, header.ImageSize);

            var documented = DocumentedSize(file.Name);
            var rebuild = RebuildSize(file.Name, bytes.Length);
            ExpectRebuildSize(file.Name, documented, rebuild);
            var stride = (documented.Width * 2 + 3) / 4 * 4;
            Equal("image_size for the documented size", documented.Height * stride, header.ImageSize);
            // The rebuild's rows must fill the pixel block exactly as well.
            Equal("image_size for the rebuild's size", rebuild.Height * ((rebuild.Width * 2 + 3) / 4 * 4),
                header.ImageSize);

            // Each pixel is RGB555 with bit 15 clear, and row padding is zero bytes.
            for (var row = 0; row < documented.Height; row++)
            {
                var line = bytes.AsSpan(54 + row * stride, stride);
                for (var x = 0; x < documented.Width; x++)
                    if ((BinaryPrimitives.ReadUInt16LittleEndian(line.Slice(x * 2, 2)) & 0x8000) != 0)
                        throw new InvalidDataException($"bit 15 is set at ({x}, {row})");
                if (line[(documented.Width * 2)..].IndexOfAnyExcept((byte)0) >= 0)
                    throw new InvalidDataException($"row {row} has padding other than zero bytes");
            }

            // The extractor's decode: BmpRepair writes the size the file lacks.
            var repaired = (byte[])bytes.Clone();
            BmpRepair.Repair(repaired, rebuild.Width, rebuild.Height, 16);
            var fixedHeader = new BitmapHeader(repaired);
            Equal("repaired width", rebuild.Width, fixedHeader.Width);
            Equal("repaired height", rebuild.Height, fixedHeader.Height);
            Equal("repaired planes", 1, fixedHeader.Planes);
            Equal("repaired bit_count", 16, fixedHeader.BitCount);
            if (!repaired.AsSpan(54).SequenceEqual(bytes.AsSpan(54)))
                throw new InvalidDataException("the repair changed pixel bytes");
        });
    }

    [Fact]
    public void Px08FilesHaveTheFmtGfx002HeaderAndDecodeToTheDocumentedSizes()
    {
        var files = OriginalFormatFiles.Require("FMT-GFX-002");
        // FMT-GFX-002, Coverage: all 214 files under DATA/PX08/.
        Assert.Equal(214, files.Count);
        // FMT-GFX-002, compression: the seven files stored as plain rows.
        var uncompressed = new HashSet<string>(StringComparer.Ordinal)
        {
            "PX05000", "PX05001", "PX05002", "PX05004", "PX05007", "PX05018", "Px05013"
        };
        var px16 = OriginalFormatFiles.Listed("FMT-GFX-001")
            .ToDictionary(file => file.Name.ToUpperInvariant(), StringComparer.Ordinal);
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var bytes = file.ReadAllBytes();
            var header = new BitmapHeader(bytes);
            var compression = uncompressed.Contains(file.Name) ? 0 : 1;
            header.Expect(pixelOffset: 1078, bitCount: 8, compression: compression, pixelsPerMeter: 2835);
            Equal("image_size", bytes.Length - 1078, header.ImageSize);

            var name = file.Name.ToUpperInvariant();
            var documented = DocumentedSize(name);
            // The extractor sizes a PX08 file by the PX16 file of the same name.
            var counterpart = px16[name];
            var counterpartPath = OriginalGameFiles.Require(OriginalFormatFiles.Build, counterpart.Path,
                counterpart.Xxh3);
            var rebuild = RebuildSize(name, new FileInfo(counterpartPath).Length);
            ExpectRebuildSize(name, documented, rebuild);

            if (compression == 1)
            {
                var lines = Rle8Lines(bytes.AsSpan(1078));
                Equal("RLE8 line count", documented.Height, lines.Count);
                var widths = lines.Distinct().ToArray();
                if (widths.Length != 1 || widths[0] != documented.Width)
                    throw new InvalidDataException(
                        $"RLE8 lines are {string.Join(", ", widths)} pixels long, expected {documented.Width}");
            }
            else
            {
                Equal("width of a plain-row file", 344, documented.Width);
                Equal("image_size of plain rows", documented.Height * documented.Width, header.ImageSize);
            }

            // The extractor's decode.
            var decoded = Px08BmpDecoder.Decode(bytes, rebuild.Width, rebuild.Height);
            var decodedHeader = new BitmapHeader(decoded);
            Equal("decoded width", rebuild.Width, decodedHeader.Width);
            Equal("decoded height", rebuild.Height, decodedHeader.Height);
            Equal("decoded length", 1078 + rebuild.Height * ((rebuild.Width + 3) & ~3), decoded.Length);
            if (compression == 0 && !decoded.AsSpan(1078).SequenceEqual(bytes.AsSpan(1078)))
                throw new InvalidDataException("the decoder changed plain pixel rows");
        });
    }

    [Fact]
    public void Px08PalettesAreTheFmtGfx003EntriesTheDecoderKeeps()
    {
        // FMT-GFX-003 covers the same files as FMT-GFX-002.
        var files = OriginalFormatFiles.Require("FMT-GFX-003");
        Assert.Equal(214, files.Count);
        var px16 = OriginalFormatFiles.Listed("FMT-GFX-001")
            .ToDictionary(file => file.Name.ToUpperInvariant(), StringComparer.Ordinal);
        OriginalFormatFiles.CheckEach(files, file =>
        {
            var bytes = file.ReadAllBytes();
            var palette = bytes.AsSpan(54, 256 * 4);
            // reserved_03 is 0 in every entry of every file.
            for (var entry = 0; entry < 256; entry++)
                if (palette[entry * 4 + 3] != 0)
                    throw new InvalidDataException($"palette entry {entry} has reserved_03 {palette[entry * 4 + 3]}");

            var name = file.Name.ToUpperInvariant();
            var counterpart = px16[name];
            var counterpartPath = OriginalGameFiles.Require(OriginalFormatFiles.Build, counterpart.Path,
                counterpart.Xxh3);
            var size = RebuildSize(name, new FileInfo(counterpartPath).Length);
            var decoded = Px08BmpDecoder.Decode(bytes, size.Width, size.Height);
            // The decoded bitmap carries the 256 entries unchanged, blue, green, red, reserved.
            if (!decoded.AsSpan(54, 256 * 4).SequenceEqual(palette))
                throw new InvalidDataException("the decoded palette differs from the file's");
        });
    }

    /// <summary>
    /// FMT-GFX-001, Coverage: the width and height of each image, taken from the entry's table.
    /// PX00131 has no PX08 file and is 640 x 460.
    /// </summary>
    private static PxDimensions.Size DocumentedSize(string name)
    {
        var upper = name.ToUpperInvariant();
        var number = int.Parse(upper.AsSpan(2), System.Globalization.CultureInfo.InvariantCulture);
        return number switch
        {
            100 or 128 or 130 or 131 or (>= 143 and <= 146) => new(640, 460),
            129 => new(512, 646),
            132 => new(108, 164),
            137 or 139 => new(220, 72),
            138 or (>= 4000 and <= 4052) => new(720, 48),
            140 => new(312, 282),
            150 => new(220, 56),
            200 => new(428, 410),
            201 => new(320, 240),
            202 or 203 => new(311, 393),
            300 => new(324, 64),
            2000 => new(120, 1408),
            3000 => new(640, 576),
            4999 => new(20, 1280),
            (>= 5000 and <= 5022) or 5024 => new(344, 209),
            (>= 6001 and <= 6007) or 6009 or 6069 => new(242, 158),
            6008 => new(241, 157),
            (>= 7000 and <= 7027) or (>= 7100 and <= 7119) or (>= 7200 and <= 7228) or (>= 7300 and <= 7320)
                => new(512, 64),
            >= 10000 and <= 10006 => new(432, 416),
            _ => throw new InvalidDataException($"{name} is not in the Coverage table of FMT-GFX-001")
        };
    }

    private static PxDimensions.Size RebuildSize(string name, long px16Bytes) =>
        PxDimensions.TryGet(name.ToUpperInvariant(), px16Bytes, out var size)
            ? size
            : throw new InvalidDataException($"the rebuild has no size for {name}");

    /// <summary>
    /// The rebuild uses the documented size for every image but PX06008. The executable reads every
    /// PX06 image, PX06008 included, as 242 x 158, and the row it reads past the pixel block has not
    /// been observed (FND-GFX-005, FMT-GFX-001 Open questions); the rebuild takes the executable's
    /// width and the file's 157 rows.
    /// </summary>
    private static void ExpectRebuildSize(string name, PxDimensions.Size documented, PxDimensions.Size rebuild)
    {
        var expected = name.Equals("PX06008", StringComparison.OrdinalIgnoreCase) ? new PxDimensions.Size(242, 157) : documented;
        if (rebuild != expected)
            throw new InvalidDataException(
                $"the rebuild sizes it {rebuild.Width} x {rebuild.Height}, expected {expected.Width} x {expected.Height}");
    }

    /// <summary>
    /// The lengths of the lines of an RLE8 block (RULE-GFX-001), which must end with the
    /// end-of-bitmap code on its last two bytes (FMT-GFX-002, Coverage).
    /// </summary>
    private static List<int> Rle8Lines(ReadOnlySpan<byte> data)
    {
        var lines = new List<int>();
        var x = 0;
        var offset = 0;
        while (true)
        {
            if (offset + 2 > data.Length) throw new InvalidDataException("the RLE8 block has no end-of-bitmap code");
            var count = data[offset];
            var value = data[offset + 1];
            offset += 2;
            if (count != 0)
            {
                x += count;
                continue;
            }
            switch (value)
            {
                case 0:
                    lines.Add(x);
                    x = 0;
                    break;
                case 1:
                    if (x != 0) lines.Add(x);
                    if (offset != data.Length)
                        throw new InvalidDataException(
                            $"the end-of-bitmap code ends at {offset} of {data.Length} RLE8 bytes");
                    return lines;
                case 2:
                    throw new InvalidDataException("the RLE8 block uses a delta code");
                default:
                    x += value;
                    offset += value + (value & 1);
                    break;
            }
        }
    }

    private static void Equal(string label, long expected, long actual)
    {
        if (expected != actual) throw new InvalidDataException($"{label} is {actual}, expected {expected}");
    }

    /// <summary>The 54-byte file and information header shared by FMT-GFX-001 and FMT-GFX-002.</summary>
    private readonly ref struct BitmapHeader(ReadOnlySpan<byte> bytes)
    {
        private readonly ReadOnlySpan<byte> _bytes = bytes;

        public int Width => I32(0x12);
        public int Height => I32(0x16);
        public int Planes => U16(0x1A);
        public int BitCount => U16(0x1C);
        public long ImageSize => U32(0x22);

        /// <summary>The header fields as the entries give them, before any repair.</summary>
        public void Expect(int pixelOffset, int bitCount, int compression, int pixelsPerMeter)
        {
            if (_bytes.Length < 54 || _bytes[0] != (byte)'B' || _bytes[1] != (byte)'M')
                throw new InvalidDataException("the file does not start with BM");
            Equal("file_size", _bytes.Length, U32(0x02));
            Equal("reserved_06", 0, U16(0x06));
            Equal("reserved_08", 0, U16(0x08));
            Equal("pixel_offset", pixelOffset, U32(0x0A));
            Equal("header_size", 40, U32(0x0E));
            Equal("width", 0, Width);
            Equal("height", 0, Height);
            Equal("planes", 255, Planes);
            Equal("bit_count", bitCount, BitCount);
            Equal("compression", compression, U32(0x1E));
            Equal("x_pixels_per_meter", pixelsPerMeter, I32(0x26));
            Equal("y_pixels_per_meter", pixelsPerMeter, I32(0x2A));
            Equal("colors_used", 0, U32(0x2E));
            Equal("colors_important", 0, U32(0x32));
        }

        private int U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(_bytes.Slice(offset, 2));
        private long U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_bytes.Slice(offset, 4));
        private int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(_bytes.Slice(offset, 4));
    }
}
