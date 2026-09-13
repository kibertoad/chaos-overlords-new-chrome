using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Core.Persistence;

/// <summary>How an archive's payload was compressed.</summary>
public enum ReplayArchiveCodec : byte
{
    /// <summary>The replay JSON, verbatim. For a payload too small for compression to pay.</summary>
    None = 0,

    /// <summary>
    /// Brotli, which is what this build writes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A whole-match journal is JSON with one 64-character state fingerprint per step. The
    /// fingerprints are incompressible and everything around them is highly repetitive, so the
    /// achievable ratio is set by the hashes rather than by the codec, and Brotli at its higher
    /// quality levels is at least a match for zstd on exactly this shape. What decides it is what
    /// each side already has: Brotli ships in .NET, in Node, and in a Worker under
    /// <c>nodejs_compat</c>, while zstd needs a package on the game side and has no Workers
    /// decoder at all. A new dependency in a game that must build offline is a real cost; a few
    /// percent of ratio on a file nobody stores by the million is not.
    /// </para>
    /// <para>
    /// <see cref="Zstd"/> is reserved so adopting it later is a codec byte and a branch, not a
    /// format break: every archive already says which codec wrote it, and a reader that meets one
    /// it does not know says so instead of guessing.
    /// </para>
    /// </remarks>
    Brotli = 1,

    /// <summary>Reserved. Nothing writes this yet; see <see cref="Brotli"/>.</summary>
    Zstd = 2
}

/// <summary>How hard to work at making an archive small.</summary>
public enum ReplayArchiveEffort
{
    /// <summary>For an archive written beside a save, where the player is waiting on it.</summary>
    Fast,

    /// <summary>For an archive about to be uploaded, where every kilobyte is somebody's uplink.</summary>
    Smallest
}

/// <summary>What a reader learned about an archive without decompressing it.</summary>
public sealed record ReplayArchiveHeader(
    int ArchiveVersion,
    ReplayArchiveCodec Codec,
    int UncompressedBytes,
    int CompressedBytes);

/// <summary>
/// A compressed, self-describing container for a match journal.
/// </summary>
/// <remarks>
/// <para>
/// A journal that can be replayed from turn one is the only form of a bug report worth having for a
/// deterministic simulation, and it grows for as long as the match does: a long game is megabytes of
/// JSON. That is too much to keep beside every save and far too much to post to a server, so what is
/// written to either is this — a fixed sixteen-byte header and a compressed body.
/// </para>
/// <para>
/// The header is what makes an archive safe to accept from somewhere else. The magic refuses a file
/// that is not one; the codec byte refuses one this build cannot read, rather than handing arbitrary
/// bytes to a decompressor; and the declared uncompressed length is checked against
/// <see cref="MaximumUncompressedBytes"/> <em>before</em> anything is decompressed and again against
/// what actually came out, so a small archive cannot claim a small size and expand without bound.
/// </para>
/// </remarks>
public static class ReplayArchive
{
    /// <summary><c>RCHJ</c>: a Chaos Overlords journal archive.</summary>
    private static readonly byte[] Magic = "RCHJ"u8.ToArray();

    /// <summary>The container's own version, independent of the replay format inside it.</summary>
    public const int CurrentArchiveVersion = 1;

    /// <summary>Magic (4) + archive version (4) + codec (1) + reserved (3) + length (4).</summary>
    public const int HeaderBytes = 16;

    /// <summary>
    /// The largest payload this will produce or accept, matching the replay reader's own ceiling.
    /// </summary>
    public const int MaximumUncompressedBytes = MatchReplaySerializer.MaximumReplayBytes;

    /// <summary>
    /// Below this, compression is skipped: a few hundred bytes of Brotli framing on a payload this
    /// small is a cost with nothing to show for it.
    /// </summary>
    private const int CompressionThresholdBytes = 512;

    /// <summary>Packs a recorder's journal into an archive.</summary>
    public static byte[] Pack(
        MatchReplayRecorder recorder, ReplayArchiveEffort effort = ReplayArchiveEffort.Fast)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        using var json = new MemoryStream();
        MatchReplaySerializer.Save(json, recorder);
        return Pack(json.GetBuffer().AsSpan(0, checked((int)json.Length)), effort);
    }

    /// <summary>Packs replay JSON that has already been serialized.</summary>
    public static byte[] Pack(
        ReadOnlySpan<byte> payload, ReplayArchiveEffort effort = ReplayArchiveEffort.Fast)
    {
        if (payload.Length > MaximumUncompressedBytes)
            throw new ArgumentOutOfRangeException(
                nameof(payload), "Journal exceeds the archive size limit.");

        var codec = payload.Length >= CompressionThresholdBytes
            ? ReplayArchiveCodec.Brotli
            : ReplayArchiveCodec.None;
        using var output = new MemoryStream(HeaderBytes + payload.Length / 4 + 64);
        WriteHeader(output, codec, payload.Length);
        if (codec == ReplayArchiveCodec.None)
        {
            output.Write(payload);
        }
        else
        {
            using var brotli = new BrotliStream(output, LevelFor(effort), leaveOpen: true);
            brotli.Write(payload);
        }
        return output.ToArray();
    }

    /// <summary>Reads an archive's header without decompressing its body.</summary>
    /// <exception cref="InvalidDataException">The bytes are not a readable archive.</exception>
    public static ReplayArchiveHeader ReadHeader(ReadOnlySpan<byte> archive)
    {
        if (archive.Length < HeaderBytes)
            throw new InvalidDataException("Journal archive is truncated.");
        if (!archive[..4].SequenceEqual(Magic))
            throw new InvalidDataException("Journal archive has the wrong signature.");
        var version = BinaryPrimitives.ReadInt32LittleEndian(archive[4..8]);
        if (version is < 1 or > CurrentArchiveVersion)
            throw new InvalidDataException($"Unsupported journal archive version {version}.");
        var codec = (ReplayArchiveCodec)archive[8];
        if (!Enum.IsDefined(codec) || codec == ReplayArchiveCodec.Zstd)
            throw new InvalidDataException($"Unsupported journal archive codec {archive[8]}.");
        var uncompressed = BinaryPrimitives.ReadInt32LittleEndian(archive[12..16]);
        if (uncompressed is < 0 || uncompressed > MaximumUncompressedBytes)
            throw new InvalidDataException("Journal archive declares an unusable payload size.");
        return new ReplayArchiveHeader(version, codec, uncompressed, archive.Length);
    }

    /// <summary>Unpacks an archive back to the replay JSON it was made from.</summary>
    /// <exception cref="InvalidDataException">The bytes are not a readable archive.</exception>
    public static byte[] Unpack(ReadOnlySpan<byte> archive)
    {
        var header = ReadHeader(archive);
        var body = archive[HeaderBytes..];
        if (header.Codec == ReplayArchiveCodec.None)
        {
            if (body.Length != header.UncompressedBytes)
                throw new InvalidDataException("Journal archive length does not match its header.");
            return body.ToArray();
        }

        var payload = new byte[header.UncompressedBytes];
        using var source = new MemoryStream(body.ToArray(), writable: false);
        using var brotli = new BrotliStream(source, CompressionMode.Decompress);
        var read = 0;
        while (read < payload.Length)
        {
            var chunk = brotli.Read(payload, read, payload.Length - read);
            if (chunk == 0) throw new InvalidDataException("Journal archive is truncated.");
            read += chunk;
        }
        // Exactly the declared length, no more: a stream that still has bytes left declared a
        // smaller payload than it carries, which is how a decompression bomb is spelled.
        if (brotli.ReadByte() != -1)
            throw new InvalidDataException("Journal archive expands beyond its declared size.");
        return payload;
    }

    /// <summary>Replays an archive and answers the match state it ends at.</summary>
    public static MatchState LoadAndReplay(ReadOnlySpan<byte> archive, OriginalData definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        using var json = new MemoryStream(Unpack(archive), writable: false);
        return MatchReplaySerializer.LoadAndReplay(json, definitions);
    }

    /// <summary>The archive's own SHA-256, lowercase hex: what a receiver checks it against.</summary>
    public static string Fingerprint(ReadOnlySpan<byte> archive) =>
        Convert.ToHexStringLower(SHA256.HashData(archive));

    /// <summary>
    /// Brotli's two useful working points, as .NET spells them since 7.0.
    /// </summary>
    /// <remarks>
    /// <see cref="CompressionLevel.Optimal"/> is quality 4 — quick enough to run on the thread that
    /// just saved a game without the player noticing. <see cref="CompressionLevel.SmallestSize"/> is
    /// quality 11, which is slow and worth it exactly once: on the copy about to cross somebody's
    /// uplink, where it runs off the game loop anyway.
    /// </remarks>
    private static CompressionLevel LevelFor(ReplayArchiveEffort effort) =>
        effort == ReplayArchiveEffort.Smallest
            ? CompressionLevel.SmallestSize
            : CompressionLevel.Optimal;

    private static void WriteHeader(Stream output, ReplayArchiveCodec codec, int uncompressedBytes)
    {
        Span<byte> header = stackalloc byte[HeaderBytes];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..8], CurrentArchiveVersion);
        header[8] = (byte)codec;
        header[9] = 0;
        header[10] = 0;
        header[11] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(header[12..16], uncompressedBytes);
        output.Write(header);
    }
}
