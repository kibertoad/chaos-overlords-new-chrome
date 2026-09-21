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
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Save path has no parent directory.", nameof(path));
        // Do not promote bytes that cannot be read back, and never replace a
        // known-good backup with a corrupt current file. A state that fails its own round trip
        // is a defect in the serializer, not a disk problem, but it reaches the player as a
        // failed save either way, so report it as one: every caller filters for IOException,
        // and an InvalidDataException escaping here ended the process at the end of the turn.
        AtomicGenerationRecovery.SaveAtomic(
            fullPath, directory, BackupSuffix,
            "The save was written but could not be read back, so it was not promoted.",
            stream => NativeSaveSerializer.Save(stream, state),
            candidate => _ = Load(candidate, state.Definitions));
    }

    /// <summary>Captures a state on the caller's thread for a later durable write.</summary>
    /// <remarks>
    /// The match state is mutable and is owned by the game loop. Capturing these bytes before
    /// dispatching disk I/O lets the worker save a coherent turn without racing the next one.
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
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Save path has no parent directory.", nameof(path));
        AtomicGenerationRecovery.SaveAtomic(
            fullPath, directory, BackupSuffix,
            "The save was written but could not be read back, so it was not promoted.",
            stream => stream.Write(snapshot.Span),
            candidate => _ = Load(candidate, definitions),
            trustExistingPrimary);
    }

    public static MatchState Load(string path, OriginalData definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(
            Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        return NativeSaveSerializer.Load(stream, definitions);
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
