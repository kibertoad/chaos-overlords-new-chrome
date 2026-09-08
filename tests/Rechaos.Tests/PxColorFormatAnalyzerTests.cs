using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

public sealed class PxColorFormatAnalyzerTests
{
    [Fact]
    public void DistinguishesRgb555FromRgb565()
    {
        var indexed = CreateBmp(bitsPerPixel: 8, pixelOffset: 1078, pixelBytes: [1, 2, 0, 0]);
        indexed[60] = 255;
        indexed[63] = 255;
        var sixteenBit = CreateBmp(bitsPerPixel: 16, pixelOffset: 54, pixelBytes: [0x00, 0x7c, 0xe0, 0x03]);

        var comparison = PxColorFormatAnalyzer.Compare(indexed, sixteenBit);

        Assert.Equal(2, comparison.PixelCount);
        Assert.Equal(0, comparison.Rgb555AbsoluteError);
        Assert.True(comparison.Rgb565AbsoluteError > 0);
        Assert.Equal(2, comparison.Rgb555ExactPixels);
        Assert.Equal(0, comparison.HighBitSetPixels);
    }

    private static byte[] CreateBmp(short bitsPerPixel, int pixelOffset, byte[] pixelBytes)
    {
        var bytes = new byte[pixelOffset + pixelBytes.Length];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BitConverter.TryWriteBytes(bytes.AsSpan(2, 4), bytes.Length);
        BitConverter.TryWriteBytes(bytes.AsSpan(10, 4), pixelOffset);
        BitConverter.TryWriteBytes(bytes.AsSpan(14, 4), 40);
        BitConverter.TryWriteBytes(bytes.AsSpan(18, 4), 2);
        BitConverter.TryWriteBytes(bytes.AsSpan(22, 4), 1);
        BitConverter.TryWriteBytes(bytes.AsSpan(26, 2), (short)1);
        BitConverter.TryWriteBytes(bytes.AsSpan(28, 2), bitsPerPixel);
        pixelBytes.CopyTo(bytes, pixelOffset);
        return bytes;
    }
}
