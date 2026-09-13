namespace Rechaos.Core.Persistence;

/// <summary>Best-effort repair of a missing or invalid primary from a verified backup.</summary>
internal static class AtomicGenerationRecovery
{
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
