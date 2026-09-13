namespace Rechaos.Core.Assets;

public sealed record SmackerMovieAudio(
    int TrackIndex,
    int SampleRate,
    int Channels,
    ReadOnlyMemory<byte> UnsignedPcm8);

public sealed record SmackerMovieFrame(
    int Index,
    TimeSpan Duration,
    SmackerDecodedVideoFrame Video,
    IReadOnlyList<SmackerPaletteColor> Palette,
    IReadOnlyList<SmackerMovieAudio> Audio);

/// <summary>Sequentially decodes one validated Smacker movie without retaining prior frames.</summary>
public sealed class SmackerMovieStream : IDisposable
{
    private readonly Stream _stream;
    private readonly SmackerVideoDecoder _videoDecoder;
    private SmackerPaletteColor[] _palette = SmackerPaletteDecoder.CreateEmpty();
    private int _nextFrameIndex;
    private bool _disposed;

    private SmackerMovieStream(Stream stream, SmackerVideoMetadata metadata)
    {
        _stream = stream;
        Metadata = metadata;
        _videoDecoder = SmackerVideoDecoder.Create(stream, metadata);
    }

    public SmackerVideoMetadata Metadata { get; }
    public bool HasNextFrame => !_disposed && _nextFrameIndex < Metadata.FrameCount;

    public static SmackerMovieStream Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var stream = File.OpenRead(path);
        try
        {
            return OpenOwned(stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    internal static SmackerMovieStream OpenOwned(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new SmackerMovieStream(stream, SmackerVideoReader.Read(stream));
    }

    public SmackerMovieFrame ReadNextFrame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!HasNextFrame)
            throw new InvalidOperationException("The Smacker movie has no remaining frames.");

        var packet = SmackerFrameDemuxer.Read(_stream, Metadata, _nextFrameIndex);
        if (!packet.PaletteChunk.IsEmpty)
            _palette = SmackerPaletteDecoder.Apply(packet.PaletteChunk.Span, _palette);
        var audio = new List<SmackerMovieAudio>(packet.AudioChunks.Count);
        foreach (var chunk in packet.AudioChunks)
        {
            var track = Metadata.AudioTracks.Single(value => value.Index == chunk.TrackIndex);
            audio.Add(new SmackerMovieAudio(
                track.Index,
                track.SampleRate,
                track.Channels,
                SmackerAudioDecoder.Decode(chunk, track)));
        }

        var frame = new SmackerMovieFrame(
            _nextFrameIndex,
            Metadata.FrameDuration,
            _videoDecoder.Decode(packet),
            _palette.ToArray(),
            audio.AsReadOnly());
        _nextFrameIndex++;
        return frame;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stream.Dispose();
    }
}

public static class SmackerPcmConversion
{
    public static byte[] ToSigned16LittleEndian(ReadOnlySpan<byte> unsignedPcm8)
    {
        var output = new byte[checked(unsignedPcm8.Length * 2)];
        for (var index = 0; index < unsignedPcm8.Length; index++)
        {
            var sample = (short)((unsignedPcm8[index] - 128) << 8);
            output[index * 2] = (byte)sample;
            output[(index * 2) + 1] = (byte)(sample >> 8);
        }
        return output;
    }
}
