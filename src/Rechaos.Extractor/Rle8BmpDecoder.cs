namespace Rechaos.Extractor;

public static class Px08BmpDecoder
{
    private const int BitmapHeaderSize = 54;
    private const int PaletteEntries = 256;
    private const int PaletteBytes = PaletteEntries * 4;

    public static byte[] Decode(byte[] source, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (source.Length < BitmapHeaderSize || source[0] != (byte)'B' || source[1] != (byte)'M')
            throw new InvalidDataException("PX08 resource is not a BMP container.");

        var pixelOffset = ReadInt32(source, 10);
        var bitsPerPixel = ReadInt16(source, 28);
        var compression = ReadInt32(source, 30);
        if (pixelOffset < BitmapHeaderSize + PaletteBytes || pixelOffset > source.Length)
            throw new InvalidDataException("PX08 palette or pixel offset is invalid.");
        if (bitsPerPixel != 8 || compression is not (0 or 1))
            throw new InvalidDataException($"PX08 must use 8-bit BI_RGB or BI_RLE8 encoding; found bpp={bitsPerPixel}, compression={compression}.");

        var stride = checked((width + 3) & ~3);
        var imageBytes = checked(stride * height);
        var output = new byte[checked(pixelOffset + imageBytes)];
        source.AsSpan(0, pixelOffset).CopyTo(output);
        if (compression == 0)
        {
            if (source.Length < output.Length)
                throw new InvalidDataException("Uncompressed PX08 pixel data is truncated.");
            source.AsSpan(pixelOffset, imageBytes).CopyTo(output.AsSpan(pixelOffset));
        }
        else
        {
            var indices = DecodeIndices(source.AsSpan(pixelOffset), width, height);
            for (var y = 0; y < height; y++)
                indices.AsSpan(y * width, width).CopyTo(output.AsSpan(pixelOffset + y * stride, width));
        }
        WriteInt32(output, 2, output.Length);
        WriteInt32(output, 18, width);
        WriteInt32(output, 22, height);
        WriteInt16(output, 26, 1);
        WriteInt16(output, 28, 8);
        WriteInt32(output, 30, 0);
        WriteInt32(output, 34, imageBytes);
        WriteInt32(output, 46, PaletteEntries);
        return output;
    }

    // RULE-GFX-001, FND-GFX-008: decodes as SetDIBits does. A run past the end of its line is cut
    // there, a delta moves right and up without writing, the data may end without the
    // end-of-bitmap code, and a pixel no code writes stays 0. A lone byte after the last code is
    // ignored, as the rule's loop does, and so is a missing pad byte after a final absolute run.
    // Only a delta or an absolute run whose own bytes the data cuts off is refused.
    private static byte[] DecodeIndices(ReadOnlySpan<byte> encoded, int width, int height)
    {
        var pixels = new byte[checked(width * height)];
        var sourceIndex = 0;
        var x = 0;
        var y = 0;
        while (sourceIndex + 1 < encoded.Length)
        {
            var count = encoded[sourceIndex++];
            var value = encoded[sourceIndex++];
            if (count != 0)
            {
                Clip(pixels, x, y, width, height, count).Fill(value);
                x += count;
                continue;
            }

            switch (value)
            {
                case 0: // End of line.
                    x = 0;
                    y++;
                    break;
                case 1: // End of bitmap.
                    return pixels;
                case 2: // Delta.
                    Require(encoded, sourceIndex, 2);
                    x += encoded[sourceIndex++];
                    y += encoded[sourceIndex++];
                    break;
                default: // Absolute run.
                    var literalCount = value;
                    Require(encoded, sourceIndex, literalCount);
                    var target = Clip(pixels, x, y, width, height, literalCount);
                    encoded.Slice(sourceIndex, target.Length).CopyTo(target);
                    x += literalCount;
                    sourceIndex += literalCount + (literalCount & 1);
                    break;
            }
        }
        return pixels;
    }

    // The part of a run of count pixels from (x, y) that lies inside the image. The unsigned
    // comparison also leaves out a position that has wrapped past int.MaxValue.
    private static Span<byte> Clip(byte[] pixels, int x, int y, int width, int height, int count)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height) return Span<byte>.Empty;
        return pixels.AsSpan(y * width + x, Math.Min(count, width - x));
    }

    private static void Require(ReadOnlySpan<byte> source, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > source.Length - count)
            throw new InvalidDataException("PX08 RLE stream is truncated.");
    }

    private static short ReadInt16(byte[] bytes, int offset) => BitConverter.ToInt16(bytes, offset);
    private static int ReadInt32(byte[] bytes, int offset) => BitConverter.ToInt32(bytes, offset);
    private static void WriteInt16(byte[] bytes, int offset, short value) =>
        BitConverter.TryWriteBytes(bytes.AsSpan(offset, sizeof(short)), value);
    private static void WriteInt32(byte[] bytes, int offset, int value) =>
        BitConverter.TryWriteBytes(bytes.AsSpan(offset, sizeof(int)), value);
}
