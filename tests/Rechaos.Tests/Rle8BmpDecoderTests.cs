using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

public sealed class Px08BmpDecoderTests
{
    [Fact]
    public void DecodesEncodedAndAbsoluteRunsWithPalettePreserved()
    {
        var source = CreateSource([
            4, 1, 0, 0,
            0, 4, 2, 3, 4, 5, 0, 0,
            0, 1
        ]);
        source[58] = 17;

        var output = Px08BmpDecoder.Decode(source, width: 4, height: 2);

        Assert.Equal(output.Length, BitConverter.ToInt32(output, 2));
        Assert.Equal(4, BitConverter.ToInt32(output, 18));
        Assert.Equal(2, BitConverter.ToInt32(output, 22));
        Assert.Equal(8, BitConverter.ToInt16(output, 28));
        Assert.Equal(0, BitConverter.ToInt32(output, 30));
        Assert.Equal(17, output[58]);
        Assert.Equal([1, 1, 1, 1, 2, 3, 4, 5], output.AsSpan(1078, 8).ToArray());
    }

    [Fact]
    public void RejectsRunsOutsideKnownDimensions()
    {
        var source = CreateSource([5, 1, 0, 1]);

        var exception = Assert.Throws<InvalidDataException>(() => Px08BmpDecoder.Decode(source, width: 4, height: 1));

        Assert.Contains("bounds", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsTruncatedAbsoluteRun()
    {
        var source = CreateSource([0, 5, 1, 2]);

        var exception = Assert.Throws<InvalidDataException>(() => Px08BmpDecoder.Decode(source, width: 5, height: 1));

        Assert.Contains("truncated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepairsUncompressedPx08WithoutChangingPaletteOrPixels()
    {
        var source = CreateSource([1, 2, 3, 4]);
        BitConverter.TryWriteBytes(source.AsSpan(30, 4), 0);

        var output = Px08BmpDecoder.Decode(source, width: 4, height: 1);

        Assert.Equal([1, 2, 3, 4], output.AsSpan(1078, 4).ToArray());
        Assert.Equal(4, BitConverter.ToInt32(output, 34));
    }

    private static byte[] CreateSource(byte[] rle)
    {
        const int pixelOffset = 1078;
        var source = new byte[pixelOffset + rle.Length];
        source[0] = (byte)'B';
        source[1] = (byte)'M';
        BitConverter.TryWriteBytes(source.AsSpan(2, 4), source.Length);
        BitConverter.TryWriteBytes(source.AsSpan(10, 4), pixelOffset);
        BitConverter.TryWriteBytes(source.AsSpan(14, 4), 40);
        BitConverter.TryWriteBytes(source.AsSpan(28, 2), (short)8);
        BitConverter.TryWriteBytes(source.AsSpan(30, 4), 1);
        rle.CopyTo(source, pixelOffset);
        return source;
    }
}
