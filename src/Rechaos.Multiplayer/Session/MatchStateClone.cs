using System.Buffers.Binary;
using System.IO.Compression;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// A deep copy of a match, through the same serializer a quick-save uses.
/// </summary>
/// <remarks>
/// <para>
/// Online play keeps two states. The authoritative one advances only by applying sealed turns, and
/// the copy is what the player plans on: queueing a command has to show up in the interface at once,
/// but applying it locally before the turn seals would put this client's state ahead of everyone
/// else's, and the sealed set would then apply it a second time.
/// </para>
/// <para>
/// Round-tripping the native format is the copy: it is the same code path the game already trusts
/// for a save and a snapshot upload, so a field it does not carry is a field no client would have
/// recovered either — a bug that would surface identically on a reload.
/// </para>
/// </remarks>
public static class MatchStateClone
{
    private const int ArchiveHeaderBytes = 12;
    private const int ArchiveVersion = 1;
    private static ReadOnlySpan<byte> ArchiveMagic => "RCHS"u8;

    /// <summary>An independent copy of <paramref name="state"/>.</summary>
    public static MatchState Of(MatchState state, OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definitions);
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, state);
        stream.Position = 0;
        return NativeSaveSerializer.Load(stream, definitions);
    }

    /// <summary>A compressed native snapshot for bootstrap or desync repair, base64-encoded.</summary>
    public static string ToBase64(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var save = new MemoryStream();
        NativeSaveSerializer.Save(save, state);
        if (save.Length > NativeSaveSerializer.MaximumSaveBytes)
            throw new InvalidDataException("Native snapshot exceeds the save size limit.");

        using var archive = new MemoryStream(ArchiveHeaderBytes + checked((int)save.Length / 4));
        Span<byte> header = stackalloc byte[ArchiveHeaderBytes];
        ArchiveMagic.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..8], ArchiveVersion);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..12], checked((int)save.Length));
        archive.Write(header);
        using (var brotli = new BrotliStream(archive, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotli.Write(save.GetBuffer().AsSpan(0, checked((int)save.Length)));
        }
        return Convert.ToBase64String(archive.GetBuffer(), 0, checked((int)archive.Length));
    }

    /// <summary>
    /// The state inside a snapshot body the server relayed. Uncompressed bodies written by older
    /// clients remain readable so an in-progress match can be resumed after an update.
    /// </summary>
    public static MatchState FromBase64(string body, OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(definitions);
        var bytes = Convert.FromBase64String(body);
        if (!bytes.AsSpan().StartsWith(ArchiveMagic))
        {
            using var legacy = new MemoryStream(bytes, writable: false);
            return NativeSaveSerializer.Load(legacy, definitions);
        }
        if (bytes.Length < ArchiveHeaderBytes)
            throw new InvalidDataException("Native snapshot archive is truncated.");
        var version = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, 4));
        if (version != ArchiveVersion)
            throw new InvalidDataException($"Unsupported native snapshot archive version {version}.");
        var uncompressedBytes = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8, 4));
        if (uncompressedBytes is < 0 or > NativeSaveSerializer.MaximumSaveBytes)
            throw new InvalidDataException("Native snapshot archive declares an unusable payload size.");

        var payload = new byte[uncompressedBytes];
        using var compressed = new MemoryStream(
            bytes, ArchiveHeaderBytes, bytes.Length - ArchiveHeaderBytes, writable: false);
        using (var brotli = new BrotliStream(compressed, CompressionMode.Decompress))
        {
            try
            {
                brotli.ReadExactly(payload);
                if (brotli.ReadByte() != -1)
                    throw new InvalidDataException(
                        "Native snapshot archive expands beyond its declared size.");
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("Native snapshot archive is truncated.", exception);
            }
            // BrotliStream reports damaged input as InvalidOperationException, which the caller of
            // this method does not filter for: a mangled snapshot body from the server would end
            // the process instead of being refused as an unusable repair candidate.
            catch (InvalidOperationException exception)
            {
                throw new InvalidDataException("Native snapshot archive body is corrupt.", exception);
            }
        }
        using var stream = new MemoryStream(payload, writable: false);
        return NativeSaveSerializer.Load(stream, definitions);
    }
}
