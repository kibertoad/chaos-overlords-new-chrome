using System.Text;
using Rechaos.Core.Assets;
using Xunit;

namespace Rechaos.Tests;

public sealed class SmackerVideoReaderTests
{
    [Fact]
    public void ReadsNegativeRateAndPackedStereoAudioMetadata()
    {
        using var stream = BuildContainer(
            width: 480,
            height: 256,
            frames: 2,
            rawFrameDuration: -10_000,
            treeBytes: 3,
            framePayloadSizes: [4, 8],
            audioBufferBytes: 48_512,
            audioInfo: 0xD000_5622);

        var metadata = SmackerVideoReader.Read(stream);

        Assert.Equal(480, metadata.Width);
        Assert.Equal(256, metadata.Height);
        Assert.Equal(2, metadata.FrameCount);
        Assert.Equal(TimeSpan.FromMilliseconds(100), metadata.FrameDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(200), metadata.Duration);
        var audio = Assert.Single(metadata.AudioTracks);
        Assert.Equal(22_050, audio.SampleRate);
        Assert.Equal(16, audio.BitsPerSample);
        Assert.Equal(2, audio.Channels);
        Assert.True(audio.IsPacked);
    }

    [Fact]
    public void AccountsForRingFrameInStoredFrameTables()
    {
        using var stream = BuildContainer(
            320, 200, frames: 1, rawFrameDuration: 50, treeBytes: 0,
            framePayloadSizes: [4, 4], flags: 1);

        var metadata = SmackerVideoReader.Read(stream);

        Assert.Equal(1, metadata.FrameCount);
        Assert.Equal(TimeSpan.FromMilliseconds(50), metadata.Duration);
    }

    [Theory]
    [InlineData("SMK4")]
    [InlineData("JUNK")]
    public void RejectsUnsupportedOrInvalidSignature(string signature)
    {
        using var stream = BuildContainer(320, 200, 1, 50, 0, [4]);
        stream.Position = 0;
        stream.Write(Encoding.ASCII.GetBytes(signature));
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => SmackerVideoReader.Read(stream));
    }

    [Fact]
    public void RejectsTruncatedFramePayload()
    {
        using var valid = BuildContainer(320, 200, 1, 50, 0, [8]);
        using var truncated = new MemoryStream(valid.ToArray()[..^1]);

        Assert.Throws<InvalidDataException>(() => SmackerVideoReader.Read(truncated));
    }

    private static MemoryStream BuildContainer(
        int width,
        int height,
        int frames,
        int rawFrameDuration,
        uint treeBytes,
        IReadOnlyList<uint> framePayloadSizes,
        uint flags = 0,
        uint audioBufferBytes = 0,
        uint audioInfo = 0)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("SMK2"u8);
            writer.Write(width);
            writer.Write(height);
            writer.Write(frames);
            writer.Write(rawFrameDuration);
            writer.Write(flags);
            writer.Write(audioBufferBytes);
            for (var index = 1; index < 7; index++) writer.Write(0u);
            writer.Write(treeBytes);
            for (var index = 0; index < 4; index++) writer.Write(0u);
            writer.Write(audioInfo);
            for (var index = 1; index < 7; index++) writer.Write(0u);
            writer.Write(0u);
            foreach (var payloadSize in framePayloadSizes) writer.Write(payloadSize);
            foreach (var _ in framePayloadSizes) writer.Write((byte)0);
            writer.Write(new byte[checked((int)treeBytes)]);
            foreach (var payloadSize in framePayloadSizes)
                writer.Write(new byte[checked((int)payloadSize)]);
        }
        stream.Position = 0;
        return stream;
    }
}
