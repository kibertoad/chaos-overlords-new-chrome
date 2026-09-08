using System.Buffers.Binary;

namespace Rechaos.Extractor;

public sealed record PxColorComparison(
    long PixelCount,
    long Rgb555AbsoluteError,
    long Rgb565AbsoluteError,
    long Rgb555ExactPixels,
    long Rgb565ExactPixels,
    long HighBitSetPixels)
{
    public static PxColorComparison operator +(PxColorComparison left, PxColorComparison right) => new(
        left.PixelCount + right.PixelCount,
        left.Rgb555AbsoluteError + right.Rgb555AbsoluteError,
        left.Rgb565AbsoluteError + right.Rgb565AbsoluteError,
        left.Rgb555ExactPixels + right.Rgb555ExactPixels,
        left.Rgb565ExactPixels + right.Rgb565ExactPixels,
        left.HighBitSetPixels + right.HighBitSetPixels);
}

public static class PxColorFormatAnalyzer
{
    public static PxColorComparison Compare(byte[] indexedBmp, byte[] sixteenBitBmp)
    {
        var indexed = ReadHeader(indexedBmp, expectedBitsPerPixel: 8);
        var sixteenBit = ReadHeader(sixteenBitBmp, expectedBitsPerPixel: 16);
        if (indexed.Width != sixteenBit.Width || indexed.Height != sixteenBit.Height)
            throw new InvalidDataException("PX08 and PX16 dimensions differ.");
        if (indexed.PixelOffset < 54 + 256 * 4)
            throw new InvalidDataException("PX08 palette is incomplete.");

        var indexedStride = checked((indexed.Width + 3) & ~3);
        var sixteenBitStride = checked((indexed.Width * 2 + 3) & ~3);
        RequirePixels(indexedBmp, indexed.PixelOffset, indexedStride, indexed.Height);
        RequirePixels(sixteenBitBmp, sixteenBit.PixelOffset, sixteenBitStride, sixteenBit.Height);

        long error555 = 0, error565 = 0, exact555 = 0, exact565 = 0, highBit = 0;
        for (var y = 0; y < indexed.Height; y++)
        for (var x = 0; x < indexed.Width; x++)
        {
            var paletteIndex = indexedBmp[indexed.PixelOffset + y * indexedStride + x];
            var paletteOffset = 54 + paletteIndex * 4;
            var blue = indexedBmp[paletteOffset];
            var green = indexedBmp[paletteOffset + 1];
            var red = indexedBmp[paletteOffset + 2];
            var valueOffset = sixteenBit.PixelOffset + y * sixteenBitStride + x * 2;
            var value = BinaryPrimitives.ReadUInt16LittleEndian(sixteenBitBmp.AsSpan(valueOffset, 2));

            var red555 = Expand5((value >> 10) & 31);
            var green555 = Expand5((value >> 5) & 31);
            var blue555 = Expand5(value & 31);
            var red565 = Expand5((value >> 11) & 31);
            var green565 = Expand6((value >> 5) & 63);
            var blue565 = blue555;
            error555 += Difference(red, green, blue, red555, green555, blue555);
            error565 += Difference(red, green, blue, red565, green565, blue565);
            if ((value & 0x7fff) == Pack555(red, green, blue)) exact555++;
            if (value == Pack565(red, green, blue)) exact565++;
            if ((value & 0x8000) != 0) highBit++;
        }

        return new PxColorComparison((long)indexed.Width * indexed.Height,
            error555, error565, exact555, exact565, highBit);
    }

    private static (int Width, int Height, int PixelOffset) ReadHeader(byte[] bytes, int expectedBitsPerPixel)
    {
        if (bytes.Length < 54 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
            throw new InvalidDataException("PX comparison input is not a BMP.");
        var width = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(18, 4));
        var height = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(22, 4));
        var bitsPerPixel = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(28, 2));
        var compression = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(30, 4));
        var pixelOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(10, 4));
        if (width <= 0 || height <= 0 || bitsPerPixel != expectedBitsPerPixel || compression != 0)
            throw new InvalidDataException($"Expected uncompressed {expectedBitsPerPixel}-bit BMP input.");
        return (width, height, pixelOffset);
    }

    private static void RequirePixels(byte[] bytes, int offset, int stride, int height)
    {
        if (offset < 0 || offset > bytes.Length - checked(stride * height))
            throw new InvalidDataException("PX comparison pixel data is truncated.");
    }

    private static int Difference(int red, int green, int blue, int otherRed, int otherGreen, int otherBlue) =>
        Math.Abs(red - otherRed) + Math.Abs(green - otherGreen) + Math.Abs(blue - otherBlue);
    private static int Expand5(int value) => (value << 3) | (value >> 2);
    private static int Expand6(int value) => (value << 2) | (value >> 4);
    private static int Quantize5(int value) => (value * 31 + 127) / 255;
    private static int Quantize6(int value) => (value * 63 + 127) / 255;
    private static int Pack555(int red, int green, int blue) =>
        (Quantize5(red) << 10) | (Quantize5(green) << 5) | Quantize5(blue);
    private static int Pack565(int red, int green, int blue) =>
        (Quantize5(red) << 11) | (Quantize6(green) << 5) | Quantize5(blue);
}
