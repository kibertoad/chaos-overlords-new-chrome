using Shared = RefurbishedDinosaurs.Media.Smacker;

namespace Rechaos.Core.Assets;

public sealed record SmackerDecodedVideoFrame(int Width, int Height, ReadOnlyMemory<byte> ColorIndices);

/// <summary>Stateful game adapter over the toolkit's Smacker video decoder.</summary>
public sealed class SmackerVideoDecoder
{
    private readonly int width;
    private readonly int height;
    private readonly Shared.SmackerVideoDecoder decoder;
    private readonly byte[] indices;

    private SmackerVideoDecoder(SmackerVideoMetadata metadata, byte[] trees)
    {
        width = metadata.Width;
        height = metadata.Height;
        decoder = Shared.SmackerVideoDecoder.FromTreeData(SharedSmacker.Movie(metadata), trees);
        indices = new byte[checked(width * height)];
    }

    public static SmackerVideoDecoder Create(Stream stream, SmackerVideoMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(metadata);
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("Smacker tree decoding requires a readable, seekable stream.", nameof(stream));
        if (metadata.Width % 4 != 0 || metadata.Height % 4 != 0)
            throw new NotSupportedException("Smacker dimensions must be divisible into 4x4 blocks.");
        if ((metadata.Flags & ~1u) != 0)
            throw new NotSupportedException("Interlaced or line-doubled Smacker video is not supported.");
        if (metadata.TreeBytes > SmackerVideoReader.MaximumTreeBytes)
            throw new InvalidDataException("Smacker Huffman trees exceed the allocation limit.");
        var trees = new byte[checked((int)metadata.TreeBytes)];
        stream.Position = metadata.TreeOffset;
        stream.ReadExactly(trees);
        return new SmackerVideoDecoder(metadata, trees);
    }

    public SmackerDecodedVideoFrame Decode(SmackerFramePacket packet)
    {
        decoder.DecodeFrame(packet.VideoData.Span, indices, packet.Descriptor.IsKeyFrame);
        return new SmackerDecodedVideoFrame(width, height, indices.ToArray());
    }
}
