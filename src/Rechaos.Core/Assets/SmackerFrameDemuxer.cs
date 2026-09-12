using System.Buffers.Binary;

namespace Rechaos.Core.Assets;

public sealed record SmackerAudioChunk(
    int TrackIndex,
    int? DecodedBytes,
    ReadOnlyMemory<byte> EncodedData);

public sealed record SmackerFramePacket(
    SmackerFrameDescriptor Descriptor,
    ReadOnlyMemory<byte> PaletteChunk,
    IReadOnlyList<SmackerAudioChunk> AudioChunks,
    ReadOnlyMemory<byte> VideoData);

/// <summary>Separates one validated physical frame into palette, audio, and video chunks.</summary>
public static class SmackerFrameDemuxer
{
    public static SmackerFramePacket Read(
        Stream stream,
        SmackerVideoMetadata metadata,
        int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(metadata);
        if (!stream.CanRead || !stream.CanSeek)
            throw new ArgumentException("Smacker frame demuxing requires a readable, seekable stream.", nameof(stream));
        if (frameIndex < 0 || frameIndex >= metadata.Frames.Count)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));

        var descriptor = metadata.Frames[frameIndex];
        if (descriptor.PayloadBytes is < 0 or > SmackerVideoReader.MaximumFramePayloadBytes)
            throw new InvalidDataException(
                $"Smacker frame {frameIndex} payload exceeds the allocation limit.");
        stream.Position = descriptor.Offset;
        var payload = new byte[descriptor.PayloadBytes];
        stream.ReadExactly(payload);
        var offset = 0;
        ReadOnlyMemory<byte> palette = ReadOnlyMemory<byte>.Empty;
        if (descriptor.HasPalette)
        {
            Require(payload, offset, 1, "palette length");
            var paletteBytes = checked(payload[offset] * 4);
            if (paletteBytes == 0)
                throw new InvalidDataException($"Smacker frame {frameIndex} has an empty palette chunk.");
            Require(payload, offset, paletteBytes, "palette chunk");
            palette = payload.AsMemory(offset, paletteBytes);
            offset += paletteBytes;
        }

        var audioChunks = new List<SmackerAudioChunk>();
        for (var trackIndex = 0; trackIndex < 7; trackIndex++)
        {
            if (!descriptor.HasAudioTrack(trackIndex)) continue;
            var track = metadata.AudioTracks.SingleOrDefault(value => value.Index == trackIndex)
                ?? throw new InvalidDataException(
                    $"Smacker frame {frameIndex} references absent audio track {trackIndex}.");
            Require(payload, offset, 4, "audio chunk length");
            var chunkBytes = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, 4));
            if (chunkBytes > int.MaxValue || chunkBytes < (track.IsPacked ? 8u : 4u))
                throw new InvalidDataException(
                    $"Smacker frame {frameIndex} audio track {trackIndex} has invalid length {chunkBytes}.");
            Require(payload, offset, checked((int)chunkBytes), "audio chunk");
            int? decodedBytes = null;
            var dataOffset = offset + 4;
            if (track.IsPacked)
            {
                var decodedByteCount = BinaryPrimitives.ReadUInt32LittleEndian(
                    payload.AsSpan(dataOffset, 4));
                if (decodedByteCount > int.MaxValue
                    || decodedByteCount > track.MaximumDecodedBytes)
                    throw new InvalidDataException(
                        $"Smacker frame {frameIndex} audio track {trackIndex} exceeds its decoded buffer.");
                decodedBytes = (int)decodedByteCount;
                dataOffset += 4;
            }
            var dataBytes = checked(offset + (int)chunkBytes - dataOffset);
            audioChunks.Add(new SmackerAudioChunk(
                trackIndex, decodedBytes, payload.AsMemory(dataOffset, dataBytes)));
            offset += checked((int)chunkBytes);
        }

        return new SmackerFramePacket(
            descriptor,
            palette,
            audioChunks.AsReadOnly(),
            payload.AsMemory(offset));
    }

    private static void Require(byte[] payload, int offset, int count, string component)
    {
        if (count < 0 || offset < 0 || offset > payload.Length - count)
            throw new InvalidDataException($"Smacker {component} exceeds its frame payload.");
    }
}
