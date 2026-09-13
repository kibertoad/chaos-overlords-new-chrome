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
    public void RejectsOversizedDecodedTreeAllocation()
    {
        using var stream = BuildContainer(320, 200, 1, 50, 0, [4]);
        stream.Position = 56;
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
            writer.Write(1_048_577u);
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => SmackerVideoReader.Read(stream));
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

    [Fact]
    public void PaletteDecoderAppliesNewColorAndRetainsSkippedColors()
    {
        var previous = Enumerable.Range(0, SmackerPaletteDecoder.ColorCount)
            .Select(index => new SmackerPaletteColor((byte)index, 2, 3))
            .ToArray();
        byte[] chunk = [2, 1, 2, 3, 0xff, 0xfe, 0, 0];

        var palette = SmackerPaletteDecoder.Apply(chunk, previous);

        Assert.Equal(new SmackerPaletteColor(12, 8, 4), palette[0]);
        Assert.Equal(previous[1], palette[1]);
        Assert.Equal(previous[255], palette[255]);
    }

    [Fact]
    public void PaletteDecoderCopiesAPreviousRange()
    {
        var previous = Enumerable.Range(0, SmackerPaletteDecoder.ColorCount)
            .Select(index => new SmackerPaletteColor((byte)index, 0, 0))
            .ToArray();
        byte[] chunk = [2, 0x41, 10, 0xff, 0xfd, 0, 0, 0];

        var palette = SmackerPaletteDecoder.Apply(chunk, previous);

        Assert.Equal(previous[10], palette[0]);
        Assert.Equal(previous[11], palette[1]);
        Assert.Equal(previous[2], palette[2]);
    }

    [Theory]
    [InlineData(new byte[] { 1, 0, 0, 0 })]
    [InlineData(new byte[] { 1, 0x7f, 0, 0 })]
    [InlineData(new byte[] { 1, 0x7f, 250, 0 })]
    public void PaletteDecoderRejectsMalformedCommands(byte[] chunk)
    {
        Assert.Throws<InvalidDataException>(() =>
            SmackerPaletteDecoder.Apply(chunk, SmackerPaletteDecoder.CreateEmpty()));
    }

    [Fact]
    public void AudioDecoderReconstructsMonoPredictiveSamples()
    {
        var bits = new List<int> { 1, 0, 0, 1, 1, 0 };
        AppendByte(bits, 1);
        bits.Add(0);
        AppendByte(bits, 255);
        bits.Add(0);
        AppendByte(bits, 10);
        bits.AddRange([0, 1, 0]);
        var chunk = new SmackerAudioChunk(0, 4, PackBits(bits));

        var output = SmackerAudioDecoder.Decode(chunk, AudioTrack(channels: 1, maximumBytes: 4));

        Assert.Equal(new byte[] { 10, 11, 10, 11 }, output);
    }

    [Fact]
    public void AudioDecoderReconstructsInterleavedStereoSamples()
    {
        var bits = new List<int> { 1, 1, 0 };
        foreach (var delta in new byte[] { 1, 2 })
        {
            bits.AddRange([1, 0]);
            AppendByte(bits, delta);
            bits.Add(0);
        }
        AppendByte(bits, 20);
        AppendByte(bits, 10);
        var chunk = new SmackerAudioChunk(0, 6, PackBits(bits));

        var output = SmackerAudioDecoder.Decode(chunk, AudioTrack(channels: 2, maximumBytes: 6));

        Assert.Equal(new byte[] { 10, 20, 11, 22, 12, 24 }, output);
    }

    [Fact]
    public void AudioDecoderRejectsTruncatedTree()
    {
        var chunk = new SmackerAudioChunk(0, 1, new byte[] { 1 });

        Assert.Throws<InvalidDataException>(() =>
            SmackerAudioDecoder.Decode(chunk, AudioTrack(channels: 1, maximumBytes: 1)));
    }

    [Fact]
    public void VideoDecoderExpandsMonochromeBlockBitsByRow()
    {
        var decoder = BuildVideoDecoder(
            monochromeMap: 0x8421,
            monochromeColor: 0x0703,
            fullBlock: 0,
            blockType: 0);

        var frame = decoder.Decode(VideoPacket());

        Assert.Equal(new byte[]
        {
            7, 3, 3, 3,
            3, 7, 3, 3,
            3, 3, 7, 3,
            3, 3, 3, 7
        }, frame.ColorIndices.ToArray());
    }

    [Fact]
    public void VideoDecoderExpandsFullAndFillBlocks()
    {
        var full = BuildVideoDecoder(0, 0, 0x0201, blockType: 1)
            .Decode(VideoPacket());
        var fill = BuildVideoDecoder(0, 0, 0, blockType: 0x0903)
            .Decode(VideoPacket());

        Assert.Equal(new byte[]
        {
            1, 2, 1, 2,
            1, 2, 1, 2,
            1, 2, 1, 2,
            1, 2, 1, 2
        }, full.ColorIndices.ToArray());
        Assert.All(fill.ColorIndices.ToArray(), value => Assert.Equal(9, value));
    }

    [Fact]
    public void VideoDecoderRejectsRunBeyondFrame()
    {
        var decoder = BuildVideoDecoder(0, 0, 0, blockType: 0x0907);

        Assert.Throws<InvalidDataException>(() => decoder.Decode(VideoPacket()));
    }

    [Fact]
    public void MovieStreamDecodesSequentialPresentationFrame()
    {
        var treeData = BuildConstantCodebooks(0, 0, 0, 0x0903);
        var stream = BuildContainer(
            4, 4, 1, 100, (uint)treeData.Length, [0],
            treeData: treeData,
            treeAllocationSizes: new SmackerHuffmanTreeSizes(4, 4, 4, 4));
        using var movie = SmackerMovieStream.OpenOwned(stream);

        var frame = movie.ReadNextFrame();

        Assert.Equal(0, frame.Index);
        Assert.Equal(TimeSpan.FromMilliseconds(100), frame.Duration);
        Assert.All(frame.Video.ColorIndices.ToArray(), value => Assert.Equal(9, value));
        Assert.Empty(frame.Audio);
        Assert.False(movie.HasNextFrame);
        Assert.Throws<InvalidOperationException>(() => movie.ReadNextFrame());
    }

    private static SmackerVideoDecoder BuildVideoDecoder(
        ushort monochromeMap,
        ushort monochromeColor,
        ushort fullBlock,
        ushort blockType)
    {
        var treeData = BuildConstantCodebooks(
            monochromeMap, monochromeColor, fullBlock, blockType);
        using var stream = new MemoryStream(treeData);
        var metadata = new SmackerVideoMetadata(
            4, 4, 1, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100),
            0, 0, (uint)treeData.Length, new SmackerHuffmanTreeSizes(4, 4, 4, 4),
            [], []);
        return SmackerVideoDecoder.Create(stream, metadata);
    }

    private static byte[] BuildConstantCodebooks(
        ushort monochromeMap,
        ushort monochromeColor,
        ushort fullBlock,
        ushort blockType)
    {
        var bits = new List<int>();
        foreach (var value in new[] { monochromeMap, monochromeColor, fullBlock, blockType })
            AppendConstantCodebook(bits, value);
        return PackBits(bits);
    }

    private static void AppendConstantCodebook(List<int> bits, ushort value)
    {
        bits.Add(1);
        bits.AddRange([1, 0]);
        AppendByte(bits, (byte)value);
        bits.Add(0);
        bits.AddRange([1, 0]);
        AppendByte(bits, (byte)(value >> 8));
        bits.Add(0);
        AppendUInt16(bits, 0xfffd);
        AppendUInt16(bits, 0xfffe);
        AppendUInt16(bits, 0xffff);
        bits.AddRange([0, 0]);
    }

    private static SmackerFramePacket VideoPacket() =>
        new(new SmackerFrameDescriptor(0, 0, 0, 0, IsKeyFrame: true),
            ReadOnlyMemory<byte>.Empty, [], ReadOnlyMemory<byte>.Empty);

    private static SmackerAudioTrack AudioTrack(int channels, int maximumBytes) =>
        new(0, (uint)maximumBytes, 22_050, 8, channels, IsPacked: true);

    private static void AppendByte(List<int> bits, byte value)
    {
        for (var index = 0; index < 8; index++) bits.Add((value >> index) & 1);
    }

    private static void AppendUInt16(List<int> bits, ushort value)
    {
        AppendByte(bits, (byte)value);
        AppendByte(bits, (byte)(value >> 8));
    }

    private static byte[] PackBits(IReadOnlyList<int> bits)
    {
        var bytes = new byte[(bits.Count + 7) / 8];
        for (var index = 0; index < bits.Count; index++)
            bytes[index / 8] |= (byte)(bits[index] << (index % 8));
        return bytes;
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
        IReadOnlyList<byte[]>? framePayloads = null,
        byte[]? treeData = null,
        SmackerHuffmanTreeSizes? treeAllocationSizes = null)
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
            var allocations = treeAllocationSizes ?? new SmackerHuffmanTreeSizes(0, 0, 0, 0);
            writer.Write(allocations.MonochromeMap);
            writer.Write(allocations.MonochromeColor);
            writer.Write(allocations.FullBlock);
            writer.Write(allocations.BlockType);
            writer.Write(audioInfo);
            for (var index = 1; index < 7; index++) writer.Write(0u);
            writer.Write(0u);
            foreach (var payloadSize in framePayloadSizes) writer.Write(payloadSize);
            foreach (var frameType in frameTypes ?? Enumerable.Repeat((byte)0, framePayloadSizes.Count))
                writer.Write(frameType);
            if (treeData is not null && treeData.Length != treeBytes)
                throw new ArgumentException("Synthetic tree data length does not match its header.", nameof(treeData));
            writer.Write(treeData ?? new byte[checked((int)treeBytes)]);
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
