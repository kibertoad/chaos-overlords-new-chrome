using System.Collections.ObjectModel;

namespace Rechaos.Core.Assets;

public sealed record SmackerAudioTrack(
    int Index,
    uint MaximumDecodedBytes,
    int SampleRate,
    int BitsPerSample,
    int Channels,
    bool IsPacked);

public sealed record SmackerVideoMetadata(
    int Width,
    int Height,
    int FrameCount,
    TimeSpan FrameDuration,
    TimeSpan Duration,
    uint Flags,
    long TreeOffset,
    uint TreeBytes,
    SmackerHuffmanTreeSizes TreeAllocationSizes,
    IReadOnlyList<SmackerAudioTrack> AudioTracks,
    IReadOnlyList<SmackerFrameDescriptor> Frames);

public sealed record SmackerHuffmanTreeSizes(
    uint MonochromeMap,
    uint MonochromeColor,
    uint FullBlock,
    uint BlockType);

public sealed record SmackerFrameDescriptor(
    int Index,
    long Offset,
    int PayloadBytes,
    byte TypeFlags,
    bool IsKeyFrame)
{
    public bool HasPalette => (TypeFlags & 1) != 0;

    public bool HasAudioTrack(int trackIndex)
    {
        if (trackIndex is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(trackIndex));
        return (TypeFlags & (1 << (trackIndex + 1))) != 0;
    }
}

/// <summary>Bounded metadata and container-layout validation for Smacker v2 files.</summary>
public static class SmackerVideoReader
{
    private const int HeaderBytes = 104;
    private const uint RingFrameFlag = 1;
    private const uint PackedAudioFlag = 0x8000_0000;
    private const uint AudioPresentFlag = 0x4000_0000;
    private const uint SixteenBitAudioFlag = 0x2000_0000;
    private const uint StereoAudioFlag = 0x1000_0000;
    private const uint SampleRateMask = 0x00ff_ffff;
    private const int MaximumDimension = 8_192;
    private const int MaximumFrames = 1_000_000;
    internal const int MaximumFramePayloadBytes = 64 * 1024 * 1024;

    public static SmackerVideoMetadata Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static SmackerVideoMetadata Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("Smacker metadata requires a readable, seekable stream.", nameof(stream));
        if (stream.Length < HeaderBytes)
            throw new InvalidDataException("Smacker file is shorter than its fixed header.");

        stream.Position = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        if (!reader.ReadBytes(4).SequenceEqual("SMK2"u8.ToArray()))
            throw new InvalidDataException("Only the supported Smacker v2 container is accepted.");

        var width = ReadBoundedInt32(reader, "width", 1, MaximumDimension);
        var height = ReadBoundedInt32(reader, "height", 1, MaximumDimension);
        var frameCount = ReadBoundedInt32(reader, "frame count", 1, MaximumFrames);
        var rawFrameDuration = reader.ReadInt32();
        if (rawFrameDuration == 0 || rawFrameDuration == int.MinValue)
            throw new InvalidDataException("Smacker frame duration is invalid.");
        var frameDuration = rawFrameDuration > 0
            ? TimeSpan.FromMilliseconds(rawFrameDuration)
            : TimeSpan.FromSeconds(-(double)rawFrameDuration / 100_000d);
        var flags = reader.ReadUInt32();

        var audioBufferSizes = new uint[7];
        for (var index = 0; index < audioBufferSizes.Length; index++)
            audioBufferSizes[index] = reader.ReadUInt32();
        var treeBytes = reader.ReadUInt32();
        var treeAllocationSizes = new SmackerHuffmanTreeSizes(
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            reader.ReadUInt32(),
            reader.ReadUInt32());

        var audioTracks = new List<SmackerAudioTrack>();
        for (var index = 0; index < audioBufferSizes.Length; index++)
        {
            var audioInfo = reader.ReadUInt32();
            var sampleRate = checked((int)(audioInfo & SampleRateMask));
            if (sampleRate == 0 && audioBufferSizes[index] == 0) continue;
            if (sampleRate == 0)
                throw new InvalidDataException($"Smacker audio track {index} has no sample rate.");
            if ((audioInfo & AudioPresentFlag) == 0 || audioBufferSizes[index] == 0)
                throw new InvalidDataException($"Smacker audio track {index} has inconsistent presence metadata.");
            audioTracks.Add(new SmackerAudioTrack(
                index,
                audioBufferSizes[index],
                sampleRate,
                (audioInfo & SixteenBitAudioFlag) != 0 ? 16 : 8,
                (audioInfo & StereoAudioFlag) != 0 ? 2 : 1,
                (audioInfo & PackedAudioFlag) != 0));
        }
        reader.ReadUInt32(); // reserved

        var storedFrameCount = checked(frameCount + ((flags & RingFrameFlag) != 0 ? 1 : 0));
        var tableBytes = checked((long)storedFrameCount * 5);
        if (HeaderBytes + tableBytes + treeBytes > stream.Length)
            throw new InvalidDataException("Smacker frame tables and trees exceed the file length.");

        var frameSizes = new uint[storedFrameCount];
        long framePayloadBytes = 0;
        for (var index = 0; index < storedFrameCount; index++)
        {
            frameSizes[index] = reader.ReadUInt32();
            var payloadBytes = frameSizes[index] & 0xffff_fffcu;
            if (payloadBytes > MaximumFramePayloadBytes)
                throw new InvalidDataException(
                    $"Smacker frame {index} payload exceeds the allocation limit.");
            framePayloadBytes = checked(framePayloadBytes + payloadBytes);
        }
        var frameTypes = reader.ReadBytes(storedFrameCount);
        if (frameTypes.Length != storedFrameCount)
            throw new InvalidDataException("Smacker frame-type table is truncated.");
        var treeOffset = stream.Position;
        var firstFrameOffset = checked(treeOffset + treeBytes);
        var structuralLength = checked(firstFrameOffset + framePayloadBytes);
        if (structuralLength != stream.Length)
            throw new InvalidDataException(
                $"Smacker structural length is {structuralLength}, but the file length is {stream.Length}.");

        var frames = new List<SmackerFrameDescriptor>(storedFrameCount);
        var frameOffset = firstFrameOffset;
        for (var index = 0; index < storedFrameCount; index++)
        {
            var payloadBytes = (int)(frameSizes[index] & 0xffff_fffcu);
            frames.Add(new SmackerFrameDescriptor(
                index, frameOffset, payloadBytes, frameTypes[index],
                index == 0 || (frameSizes[index] & 1) != 0));
            frameOffset = checked(frameOffset + payloadBytes);
        }

        return new SmackerVideoMetadata(
            width,
            height,
            frameCount,
            frameDuration,
            TimeSpan.FromTicks(checked(frameDuration.Ticks * frameCount)),
            flags,
            treeOffset,
            treeBytes,
            treeAllocationSizes,
            new ReadOnlyCollection<SmackerAudioTrack>(audioTracks),
            new ReadOnlyCollection<SmackerFrameDescriptor>(frames));
    }

    private static int ReadBoundedInt32(
        BinaryReader reader,
        string label,
        int minimum,
        int maximum)
    {
        var value = reader.ReadInt32();
        if (value < minimum || value > maximum)
            throw new InvalidDataException(
                $"Smacker {label} {value} is outside {minimum} through {maximum}.");
        return value;
    }
}
