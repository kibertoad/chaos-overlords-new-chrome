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
        Assert.Equal(8, audio.BitsPerSample);
        Assert.Equal(2, audio.Channels);
        Assert.True(audio.IsPacked);
        Assert.Equal(114, metadata.TreeOffset);
        Assert.Equal(3u, metadata.TreeBytes);
        Assert.Equal(2, metadata.Frames.Count);
        Assert.True(metadata.Frames[0].IsKeyFrame);
        Assert.False(metadata.Frames[1].IsKeyFrame);
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

    [Fact]
    public void DemuxesPalettePackedAudioAndVideoInContainerOrder()
    {
        byte[] payload =
        [
            1, 0x80, 0, 0,
            12, 0, 0, 0,
            4, 0, 0, 0,
            9, 8, 7, 6,
            5, 4, 3, 2
        ];
        using var stream = BuildContainer(
            480, 256, 1, -10_000, 0, [20],
            audioBufferBytes: 48_512, audioInfo: 0xD000_5622,
            frameTypes: [3], framePayloads: [payload]);
        var metadata = SmackerVideoReader.Read(stream);

        var packet = SmackerFrameDemuxer.Read(stream, metadata, 0);

        Assert.Equal(new byte[] { 1, 0x80, 0, 0 }, packet.PaletteChunk.ToArray());
        var audio = Assert.Single(packet.AudioChunks);
        Assert.Equal(0, audio.TrackIndex);
        Assert.Equal(4, audio.DecodedBytes);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, audio.EncodedData.ToArray());
        Assert.Equal(new byte[] { 5, 4, 3, 2 }, packet.VideoData.ToArray());
    }

    [Fact]
    public void DemuxerRejectsAudioChunkBeyondFrameBoundary()
    {
        byte[] payload = [32, 0, 0, 0, 4, 0, 0, 0];
        using var stream = BuildContainer(
            320, 200, 1, 50, 0, [8],
            audioBufferBytes: 16, audioInfo: 0xC000_5622,
            frameTypes: [2], framePayloads: [payload]);
        var metadata = SmackerVideoReader.Read(stream);

        Assert.Throws<InvalidDataException>(() =>
            SmackerFrameDemuxer.Read(stream, metadata, 0));
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
        uint audioInfo = 0,
        IReadOnlyList<byte>? frameTypes = null,
        IReadOnlyList<byte[]>? framePayloads = null)
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
            foreach (var frameType in frameTypes ?? Enumerable.Repeat((byte)0, framePayloadSizes.Count))
                writer.Write(frameType);
            writer.Write(new byte[checked((int)treeBytes)]);
            if (framePayloads is null)
                foreach (var payloadSize in framePayloadSizes)
                    writer.Write(new byte[checked((int)(payloadSize & 0xffff_fffcu))]);
            else
                foreach (var payload in framePayloads) writer.Write(payload);
        }
        stream.Position = 0;
        return stream;
    }
}
