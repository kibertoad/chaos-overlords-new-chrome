using System.Buffers.Binary;
using Shared = RefurbishedDinosaurs.Media.Smacker;

namespace Rechaos.Core.Assets;

/// <summary>Admits the game's packed 8-bit tracks and delegates decoding to the toolkit.</summary>
public static class SmackerAudioDecoder
{
    public static byte[] Decode(SmackerAudioChunk chunk, SmackerAudioTrack track)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(track);
        if (chunk.TrackIndex != track.Index)
            throw new ArgumentException("Smacker audio chunk and track indices do not match.", nameof(chunk));
        if (!track.IsPacked || track.BitsPerSample != 8 || track.Channels is < 1 or > 2)
            throw new NotSupportedException("Only packed mono or stereo unsigned 8-bit Smacker audio is supported.");
        if (chunk.DecodedBytes is not int size || size < 0 || size > track.MaximumDecodedBytes
            || size % track.Channels != 0)
            throw new InvalidDataException("Smacker audio chunk has an invalid decoded length.");
        // The local demuxer exposes the payload without its decoded-length word.
        var packet = new byte[checked(chunk.EncodedData.Length + 4)];
        BinaryPrimitives.WriteInt32LittleEndian(packet, size);
        chunk.EncodedData.Span.CopyTo(packet.AsSpan(4));
        var pcm = Shared.SmackerAudioDecoder.DecodePcm16(packet, SharedSmacker.Track(track));
        var output = new byte[pcm.Samples.Length / 2];
        for (var i = 0; i < output.Length; i++)
            output[i] = (byte)((BinaryPrimitives.ReadInt16LittleEndian(pcm.Samples.AsSpan(i * 2)) >> 8) + 128);
        return output;
    }
}
