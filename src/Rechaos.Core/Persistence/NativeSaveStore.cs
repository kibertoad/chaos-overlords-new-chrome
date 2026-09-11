using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;

namespace Rechaos.Core.Persistence;

public sealed record NativeSaveLoadResult(MatchState State, bool RecoveredFromBackup);

/// <summary>Crash-resistant file operations for recreation-native snapshots.</summary>
public static class NativeSaveStore
{
    public const string BackupSuffix = ".bak";

    public static void SaveAtomic(string path, MatchState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(state);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Save path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, FileOptions.WriteThrough))
            {
                NativeSaveSerializer.Save(stream, state);
                stream.Flush(flushToDisk: true);
            }

            // Do not promote bytes that cannot be read back, and never replace a
            // known-good backup with a corrupt current file.
            _ = Load(temporaryPath, state.Definitions);
            if (!File.Exists(fullPath))
            {
                File.Move(temporaryPath, fullPath);
            }
            else if (IsValid(fullPath, state.Definitions))
            {
                File.Replace(temporaryPath, fullPath, fullPath + BackupSuffix);
            }
            else
            {
                File.Move(temporaryPath, fullPath, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static MatchState Load(string path, OriginalData definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(
            Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        return NativeSaveSerializer.Load(stream, definitions);
    }

    public static NativeSaveLoadResult LoadRecoveringBackup(string path, OriginalData definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(definitions);
        try
        {
            return new NativeSaveLoadResult(Load(path, definitions), false);
        }
        catch (Exception primaryFailure) when (primaryFailure is IOException or InvalidDataException)
        {
            var backupPath = Path.GetFullPath(path) + BackupSuffix;
            if (!File.Exists(backupPath)) throw;
            return new NativeSaveLoadResult(Load(backupPath, definitions), true);
        }
    }

    private static bool IsValid(string path, OriginalData definitions)
    {
        try
        {
            _ = Load(path, definitions);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return false;
        }
    }
}
