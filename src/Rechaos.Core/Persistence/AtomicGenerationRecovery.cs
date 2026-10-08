using System.Runtime.ExceptionServices;
using RefurbishedDinosaurs.Core.Persistence;

namespace Rechaos.Core.Persistence;

/// <summary>
/// The game's admission rules for the toolkit's generation recovery, which writes, reads and repairs
/// saves and replays.
/// </summary>
internal static class AtomicGenerationRecovery
{
    /// <summary>Where a rejected primary is kept instead of being overwritten.</summary>
    public const string RejectedSuffix = ".corrupt";

    /// <summary>
    /// Writes a new generation beside <paramref name="fullPath"/>, reads it back with
    /// <paramref name="load"/>, and only then promotes it.
    /// </summary>
    /// <remarks>
    /// The shared writer renames a readable primary, or one <see cref="IncompatibleSave"/>
    /// recognises, to <paramref name="backupSuffix"/> in the same <see cref="File.Replace(string, string, string?, bool)"/>
    /// that moves the new file over the primary, so the primary path always names a whole save. A
    /// damaged primary is overwritten and the existing backup stays, so a known-good backup is
    /// never replaced by a damaged file. The caller validates its own arguments and
    /// resolves <paramref name="fullPath"/>.
    /// </remarks>
    /// <param name="trustExistingPrimary">
    /// Whether the existing primary may become the backup generation without being read back
    /// first. Only a caller that knows this process wrote and verified that exact file may
    /// set it, and it has to stop setting it the moment anything else writes the path or leaves
    /// it damaged: a trusted primary replaces the known-good backup unexamined.
    /// </param>
    /// <exception cref="IOException">
    /// The new file failed its read-back; the message is <paramref name="unreadableMessage"/>.
    /// </exception>
    public static void SaveAtomic(
        string fullPath,
        string backupSuffix,
        string unreadableMessage,
        Action<Stream> write,
        Action<string> load,
        bool trustExistingPrimary = false)
    {
        // The shared writer validates the primary under Path.GetFullPath of the path it is given.
        // Only the staged file's failure is wrapped: the primary's InvalidDataException has to
        // reach IncompatibleSave.IsIncompatible unwrapped, or a newer build's save is overwritten
        // without being kept as the backup.
        var primaryPath = Path.GetFullPath(fullPath);
        RecoverableFile.Write(primaryPath, write, candidate =>
        {
            try { load(candidate); }
            catch (InvalidDataException error) when (candidate != primaryPath)
            { throw new IOException(unreadableMessage, error); }
        }, error => error is IOException or InvalidDataException,
            IncompatibleSave.IsIncompatible, backupSuffix, trustExistingPrimary);
    }

    /// <summary>
    /// Loads <paramref name="path"/>, and when it is damaged loads its backup generation instead,
    /// restoring the primary from the backup when <paramref name="repairPrimary"/> is set.
    /// </summary>
    /// <remarks>
    /// A failure <see cref="IncompatibleSave"/> recognises is rethrown without touching either file.
    /// When the backup is missing or damaged too, the primary's own failure is rethrown, so callers
    /// filter for it and ask <see cref="IncompatibleSave"/> about the file they asked for. A repair
    /// keeps the rejected primary at <see cref="RejectedSuffix"/>; one that fails still returns the
    /// backup's value, with <c>PrimaryRepaired</c> false.
    /// </remarks>
    public static (T Value, bool RecoveredFromBackup, bool PrimaryRepaired) LoadRecoveringBackup<T>(
        string path,
        string backupSuffix,
        bool repairPrimary,
        Func<string, T> load)
    {
        try
        {
            var result = repairPrimary
                ? RecoverableFile.ReadAndRepair(path, load, IsDamage, backupSuffix, RejectedSuffix)
                : RecoverableFile.Read(path, load, IsDamage, backupSuffix);
            return (result.Value, result.Generation == FileGeneration.Backup, result.PrimaryRepaired);
        }
        catch (FileGenerationsUnreadableException neither)
        {
            ExceptionDispatchInfo.Throw(neither.PrimaryFailure);
            throw;
        }
    }

    /// <summary>A failure that a backup generation may stand in for.</summary>
    private static bool IsDamage(Exception error) =>
        error is IOException or InvalidDataException && !IncompatibleSave.IsIncompatible(error);
}
