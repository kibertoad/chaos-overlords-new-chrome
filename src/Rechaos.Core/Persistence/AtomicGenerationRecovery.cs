namespace Rechaos.Core.Persistence;

/// <summary>Best-effort repair of a missing or invalid primary from a verified backup.</summary>
internal static class AtomicGenerationRecovery
{
    /// <summary>Where a rejected primary is kept instead of being overwritten.</summary>
    public const string RejectedSuffix = ".corrupt";

    /// <summary>
    /// Writes a new generation beside <paramref name="fullPath"/>, reads it back with
    /// <paramref name="load"/>, and only then promotes it.
    /// </summary>
    /// <remarks>
    /// A readable primary moves to <paramref name="backupSuffix"/> through
    /// <see cref="File.Replace(string, string, string)"/>; an unreadable one is overwritten, so a
    /// known-good backup is never replaced by a damaged file. The caller validates its own
    /// arguments and resolves <paramref name="fullPath"/> and <paramref name="directory"/>.
    /// </remarks>
    /// <param name="trustExistingPrimary">
    /// Whether the existing primary may be demoted to the backup generation without being read
    /// back first. Only a caller that knows this process wrote and verified that exact file may
    /// set it, and it has to stop setting it the moment anything else writes the path or leaves
    /// it damaged: a trusted primary is moved over the known-good backup unexamined.
    /// </param>
    /// <exception cref="IOException">
    /// The new file failed its read-back; the message is <paramref name="unreadableMessage"/>.
    /// </exception>
    public static void SaveAtomic(
        string fullPath,
        string directory,
        string backupSuffix,
        string unreadableMessage,
        Action<Stream> write,
        Action<string> load,
        bool trustExistingPrimary = false)
    {
        RefurbishedDinosaurs.Core.Persistence.RecoverableFile.Write(fullPath, write, candidate =>
        {
            try { load(candidate); }
            catch (InvalidDataException error) when (candidate != fullPath)
            { throw new IOException(unreadableMessage, error); }
        }, error => error is IOException or InvalidDataException,
            IncompatibleSave.IsIncompatible, backupSuffix, trustExistingPrimary);
    }

    /// <summary>
    /// Loads <paramref name="path"/>, and when it is damaged loads its backup generation instead,
    /// rewriting the primary from the backup when <paramref name="repairPrimary"/> is set.
    /// </summary>
    /// <remarks>
    /// A failure <see cref="IncompatibleSave"/> recognises is rethrown without touching the backup.
    /// When the backup is missing the primary's own failure is rethrown.
    /// </remarks>
    public static (T Value, bool RecoveredFromBackup, bool PrimaryRepaired) LoadRecoveringBackup<T>(
        string path,
        string backupSuffix,
        bool repairPrimary,
        Func<string, T> load)
    {
        try
        {
            return (load(path), false, false);
        }
        catch (Exception primaryFailure) when (
            primaryFailure is IOException or InvalidDataException
            && !IncompatibleSave.IsIncompatible(primaryFailure))
        {
            var backupPath = Path.GetFullPath(path) + backupSuffix;
            if (!File.Exists(backupPath)) throw;
            var value = load(backupPath);
            if (!repairPrimary) return (value, true, false);
            var fullPath = Path.GetFullPath(path);
            var repaired = TryRestore(fullPath, backupPath, candidate => _ = load(candidate));
            return (value, true, repaired);
        }
    }

    public static bool TryRestore(
        string primaryPath,
        string backupPath,
        Action<string> validate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        ArgumentNullException.ThrowIfNull(validate);
        var directory = Path.GetDirectoryName(primaryPath)
            ?? throw new ArgumentException("Primary path has no parent directory.", nameof(primaryPath));
        var temporaryPath = Path.Combine(
            directory, $".{Path.GetFileName(primaryPath)}.{Guid.NewGuid():N}.recovery.tmp");
        try
        {
            using (var input = new FileStream(
                       backupPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(
                       temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       bufferSize: 81920, FileOptions.WriteThrough))
            {
                input.CopyTo(output);
                output.Flush(flushToDisk: true);
            }
            validate(temporaryPath);
            // Keep the file being replaced. It costs one more file on disk and it is the only
            // evidence of what went wrong; overwriting it threw that away every time.
            if (File.Exists(primaryPath))
                File.Move(primaryPath, primaryPath + RejectedSuffix, overwrite: true);
            File.Move(temporaryPath, primaryPath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException
                                          or InvalidDataException
                                          or UnauthorizedAccessException
                                          or NotSupportedException)
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException)
            {
                // A valid in-memory recovery must survive best-effort repair failure.
            }
        }
    }
}
