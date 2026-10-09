using System.Buffers.Binary;
using System.IO.Compression;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Core.Persistence;

public sealed record NativeSaveLoadResult(
    MatchState State,
    bool RecoveredFromBackup,
    bool PrimaryRepaired = false);

/// <summary>Crash-resistant file operations for recreation-native snapshots.</summary>
public static class NativeSaveStore
{
    public const string BackupSuffix = ".bak";

    /// <summary>
    /// Deletes temporary files a killed process left behind in a save directory.
    /// </summary>
    /// <remarks>
    /// Every atomic writer in this namespace creates <c>.&lt;name&gt;.&lt;guid&gt;.tmp</c> beside
    /// its target and renames it into place. A process killed between the two leaves the file, up to
    /// 16 or 32 MiB of it, and nothing used to remove it. Best effort throughout: this runs at
    /// startup and must never be the reason the game does not open.
    /// </remarks>
    public static void DeleteStaleTemporaryFiles(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        try
        {
            if (!Directory.Exists(directory)) return;
            foreach (var path in Directory.EnumerateFiles(directory, ".*.tmp"))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception exception) when (exception is IOException
                                                  or UnauthorizedAccessException)
                {
                    // A file another process still holds is not this one's to remove.
                }
            }
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException or ArgumentException)
        {
            // Nothing here is required for the game to run.
        }
    }

    public static void SaveAtomic(string path, MatchState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(state);
        var fullPath = Path.GetFullPath(path);
        if (Path.GetDirectoryName(fullPath) is null)
            throw new ArgumentException("Save path has no parent directory.", nameof(path));
        // Do not promote bytes that cannot be read back, and never replace a
        // known-good backup with a corrupt current file. A state that fails its own round trip
        // is a defect in the serializer, not a disk problem, but it reaches the player as a
        // failed save either way, so report it as one: every caller filters for IOException,
        // and an InvalidDataException escaping here ended the process at the end of the turn.
        AtomicGenerationRecovery.SaveAtomic(
            fullPath, BackupSuffix,
            "The save was written but could not be read back, so it was not promoted.",
            stream => WriteFile(stream, Serialize(state)),
            candidate => _ = Load(candidate, state.Definitions));
    }

    /// <summary>Captures a state on the caller's thread for a later durable write.</summary>
    /// <remarks>
    /// The match state is mutable and is owned by the game loop. Capturing these bytes before
    /// dispatching disk I/O lets the worker save a coherent turn without racing the next one. The
    /// bytes are the snapshot JSON; <see cref="SaveAtomic(string, ReadOnlyMemory{byte}, OriginalData, bool)"/>
    /// compresses them on the worker, so the game loop pays for the JSON only.
    /// </remarks>
    public static byte[] Serialize(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, state);
        return stream.ToArray();
    }

    /// <summary>Durably saves an already captured snapshot.</summary>
    /// <param name="trustExistingPrimary">
    /// True only when this process wrote and verified the current primary immediately before this
    /// generation. It avoids reloading that known-good generation before replacing it.
    /// </param>
    public static void SaveAtomic(
        string path,
        ReadOnlyMemory<byte> snapshot,
        OriginalData definitions,
        bool trustExistingPrimary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(definitions);
        var fullPath = Path.GetFullPath(path);
        if (Path.GetDirectoryName(fullPath) is null)
            throw new ArgumentException("Save path has no parent directory.", nameof(path));
        AtomicGenerationRecovery.SaveAtomic(
            fullPath, BackupSuffix,
            "The save was written but could not be read back, so it was not promoted.",
            stream => WriteFile(stream, snapshot.Span),
            candidate => _ = Load(candidate, definitions),
            trustExistingPrimary);
    }

    public static MatchState Load(string path, OriginalData definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // Delete sharing lets a write's File.Replace promotion rename this generation to the backup
        // while it is read; without it the promotion fails on Windows.
        using var stream = new FileStream(
            Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        using var json = ReadFile(stream);
        return NativeSaveSerializer.Load(json, definitions);
    }

    /// <summary><c>RCHN</c>: a Chaos Overlords native save file.</summary>
    private static ReadOnlySpan<byte> FileMagic => "RCHN"u8;

    /// <summary>Magic (4) + codec (1) + reserved (3) + JSON length (4).</summary>
    internal const int FileHeaderBytes = 12;

    /// <summary>The body is Brotli. The byte leaves room for another codec without a format break.</summary>
    private const byte BrotliCodec = 1;

    /// <summary>
    /// The largest file <see cref="ReadFile"/> accepts: a snapshot at its limit stored as plain
    /// JSON, which is larger than any compressed body, plus the header.
    /// </summary>
    private const int MaximumFileBytes = FileHeaderBytes + NativeSaveSerializer.MaximumSaveBytes;

    /// <summary>
    /// Writes snapshot JSON as a save file: the header, then the JSON compressed with Brotli.
    /// </summary>
    /// <remarks>
    /// The JSON repeats the same member names and small numbers throughout, and the event history
    /// of a long match is most of it, so Brotli at quality 4 (<see cref="CompressionLevel.Optimal"/>)
    /// shrinks it several times over at a cost the autosave worker can carry.
    /// </remarks>
    internal static void WriteFile(Stream destination, ReadOnlySpan<byte> json)
    {
        if (json.Length > NativeSaveSerializer.MaximumSaveBytes)
            throw new InvalidDataException(
                $"Native save is {json.Length} bytes, over the {NativeSaveSerializer.MaximumSaveBytes} byte limit.");
        Span<byte> header = stackalloc byte[FileHeaderBytes];
        FileMagic.CopyTo(header);
        header[4] = BrotliCodec;
        BinaryPrimitives.WriteInt32LittleEndian(header[8..12], json.Length);
        destination.Write(header);
        using var brotli = new BrotliStream(destination, CompressionLevel.Optimal, leaveOpen: true);
        brotli.Write(json);
    }

    /// <summary>
    /// Reads a save file back to the snapshot JSON it holds, rewound and ready for
    /// <see cref="NativeSaveSerializer.Load(Stream, OriginalData)"/>.
    /// </summary>
    /// <remarks>
    /// A file without the signature is handed on as it stands. Builds before the compressed file
    /// wrote the JSON bare, and the serializer reads its format version from it: a save in the
    /// current version loads, and any other is refused as an older or newer format, never as
    /// damage.
    /// </remarks>
    internal static MemoryStream ReadFile(Stream source)
    {
        var file = NativeSaveSerializer.ReadBounded(
            source, MaximumFileBytes, "Native save exceeds the size limit.");
        var bytes = file.GetBuffer().AsSpan(0, checked((int)file.Length));
        if (!bytes.StartsWith(FileMagic)) return file;
        using (file)
        {
            if (bytes.Length < FileHeaderBytes)
                throw new InvalidDataException("Native save file is truncated.");
            if (bytes[4] != BrotliCodec)
                throw new InvalidDataException($"Native save file codec {bytes[4]} is not supported.");
            var length = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..12]);
            if (length is < 0 or > NativeSaveSerializer.MaximumSaveBytes)
                throw new InvalidDataException("Native save file declares an unusable size.");
            var json = new byte[length];
            file.Position = FileHeaderBytes;
            try
            {
                using var brotli = new BrotliStream(file, CompressionMode.Decompress, leaveOpen: true);
                brotli.ReadExactly(json);
                // A body that expands past its declared length is refused rather than read on.
                if (brotli.ReadByte() != -1)
                    throw new InvalidDataException("Native save file expands beyond its declared size.");
            }
            catch (EndOfStreamException exception)
            {
                // An IOException means the disk failed; a short body is a damaged file.
                throw new InvalidDataException("Native save file is truncated.", exception);
            }
            catch (InvalidOperationException exception)
            {
                // BrotliStream reports damaged input this way, which no caller on the load path
                // filters for.
                throw new InvalidDataException("Native save file body is corrupt.", exception);
            }
            return new MemoryStream(json, 0, json.Length, writable: false, publiclyVisible: true);
        }
    }

    /// <summary>
    /// Loads a slot, falling back to its backup generation when the primary is damaged.
    /// </summary>
    /// <param name="path">The primary file.</param>
    /// <param name="definitions">The bundled definitions this build plays with.</param>
    /// <param name="repairPrimary">
    /// Whether a successful backup load may also rewrite the primary. The save browser lists nine
    /// slots every time it opens, and listing must not change anything on disk, so it passes false.
    /// </param>
    /// <remarks>
    /// A file <see cref="IncompatibleSave"/> recognises is deliberately not recovered from. A save
    /// from a newer build, or one written against different definitions, is intact: falling back to
    /// the backup would hand the player an older match, and repairing from it would destroy the
    /// newer file.
    /// </remarks>
    public static NativeSaveLoadResult LoadRecoveringBackup(
        string path,
        OriginalData definitions,
        bool repairPrimary = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(definitions);
        var (state, recovered, repaired) = AtomicGenerationRecovery.LoadRecoveringBackup(
            path, BackupSuffix, repairPrimary, candidate => Load(candidate, definitions));
        return new NativeSaveLoadResult(state, recovered, repaired);
    }
}
