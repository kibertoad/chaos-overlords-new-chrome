using Rechaos.Core.GameModel;

namespace Rechaos.Core.Persistence;

/// <summary>
/// The compressed journal that travels beside a save.
/// </summary>
/// <remarks>
/// <para>
/// A save is where the match is; a journal is how it got there. Keeping both is what lets a bug
/// report replay a session from its first turn rather than from whenever the player last loaded,
/// and a companion file is the way to keep them: the save format is unchanged, an old save still
/// loads, and a player who deletes one has lost a reproduction rather than a game.
/// </para>
/// <para>
/// Unlike a save or a hand-written replay, a missing or unreadable journal is not a failure. Every
/// read here answers null instead of throwing, because the only thing lost is history nobody has
/// asked for yet, and a corrupt companion must never be the reason a save will not load.
/// </para>
/// </remarks>
public static class MatchJournalStore
{
    /// <summary>The companion file for <paramref name="savePath"/>.</summary>
    public static string PathFor(string savePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(savePath);
        return savePath + ".rchjournal";
    }

    /// <summary>Writes the journal atomically, replacing any older one.</summary>
    public static void SaveAtomic(string path, MatchReplayRecorder recorder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(recorder);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Journal path has no parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var archive = ReplayArchive.Pack(recorder);
            using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, FileOptions.WriteThrough))
            {
                stream.Write(archive);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    /// <summary>Removes the companion, so a save can never be paired with somebody else's history.</summary>
    public static void Delete(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            File.Delete(Path.GetFullPath(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort. A stale companion that could not be deleted is still refused on load,
            // where the journal's own end state is held against the save's.
        }
    }

    /// <summary>
    /// Loads a journal and answers a recorder that continues it, or null when there is none to
    /// continue.
    /// </summary>
    /// <remarks>
    /// The journal is adopted onto the save's own state rather than replayed onto a rebuilt one —
    /// the save is already that state, and re-deriving it would re-run the whole match on the thread
    /// the player is waiting on. See <see cref="MatchReplaySerializer.TryResumeOnto"/>.
    /// </remarks>
    /// <param name="loaded">
    /// The state just restored from the save this journal is supposed to belong to. A journal that
    /// ends anywhere else is a leftover from another game in the same slot, and is ignored: resuming
    /// it would append this match's turns to that one's history.
    /// </param>
    public static MatchReplayRecorder? TryResumeOnto(string path, MatchState loaded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(loaded);
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) return null;
            // Read through the file rather than into memory: the archive's header is what says how
            // large the payload may be, and a reader that has already loaded the file cannot
            // un-spend that. See ReplayArchive.MaximumArchiveBytes.
            using var file = new FileStream(
                fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 81920, FileOptions.SequentialScan);
            using var json = new MemoryStream(ReplayArchive.Unpack(file), writable: false);
            return MatchReplaySerializer.TryResumeOnto(json, loaded);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                          or UnauthorizedAccessException)
        {
            // The archive header bounds the payload before a byte of it is decompressed, so a
            // hostile companion file cannot get past this as an allocation rather than as bad data.
            return null;
        }
    }
}
