using System.Globalization;
using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

/// <summary>What the browser may do with a row.</summary>
public enum SaveSlotStatus
{
    /// <summary>The file loads on this build.</summary>
    Playable,

    /// <summary>The file is intact but belongs to another build or another definition set.</summary>
    Incompatible,

    /// <summary>The file exists and cannot be read.</summary>
    Unreadable
}

public sealed record SaveSlotSummary(
    int Slot,
    string Name,
    DateTimeOffset Timestamp,
    ScenarioId Scenario,
    int HumanPlayers,
    int AiPlayers,
    string MatchType,
    AiPolicyMode AiPolicy,
    bool RecoveredFromBackup = false,
    bool PrimaryRepaired = false,
    SaveSlotStatus Status = SaveSlotStatus.Playable)
{
    public bool IsPlayable => Status == SaveSlotStatus.Playable;

    /// <summary>
    /// The second line of a browser row.
    /// </summary>
    /// <remarks>
    /// Invariant culture on purpose: on a culture whose default calendar is not Gregorian the
    /// timestamp read as year 2569 or 1448, and nothing parses this string back.
    /// </remarks>
    public string Details => Status switch
    {
        SaveSlotStatus.Incompatible => "SAVED BY ANOTHER BUILD  CANNOT BE LOADED HERE",
        SaveSlotStatus.Unreadable => "FILE CANNOT BE READ  NOT SAFE TO OVERWRITE",
        _ => $"{Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}  "
             + $"{Scenario.ToString().ToUpperInvariant()}  "
             + $"{MatchType}  H{HumanPlayers} A{AiPlayers}  {AiPolicyPresentation.Label(AiPolicy)} AI"
             + (RecoveredFromBackup
                 ? PrimaryRepaired ? "  RECOVERED" : "  BACKUP ONLY"
                 : string.Empty)
    };
}

public static class SaveSlotCatalog
{
    public const int SlotCount = 9;

    /// <summary>The autosave's row in the browser, after the nine manual slots.</summary>
    public const int AutoSaveRow = SlotCount;

    /// <summary>Rows the browser draws: the nine slots plus the autosave.</summary>
    public const int BrowserRowCount = SlotCount + 1;

    public static string SavePath(string directory, int slot)
    {
        ValidateSlot(slot);
        return Path.Combine(directory, $"save-slot-{slot + 1}.rchsave");
    }

    /// <summary>
    /// Summarises a slot for the browser, without touching anything on disk.
    /// </summary>
    /// <remarks>
    /// Listing must not repair. The browser reads all nine slots every time it opens, so a repair
    /// here ran without the player choosing anything: an older build opening the load screen
    /// replaced a newer primary with the older backup beside it. A row that cannot be read is now
    /// reported as such rather than as an empty slot the player is invited to save over.
    /// </remarks>
    public static SaveSlotSummary? Read(
        string directory, int slot, OriginalData definitions) =>
        ReadFile(SavePath(directory, slot), slot, definitions);

    /// <summary>The autosave as a browser row, or null when there is none.</summary>
    public static SaveSlotSummary? ReadAutoSave(string autoSavePath, OriginalData definitions) =>
        ReadFile(autoSavePath, AutoSaveRow, definitions) is { } summary
            ? summary with { Name = "AUTOSAVE" }
            : null;

    private static SaveSlotSummary? ReadFile(string path, int row, OriginalData definitions)
    {
        if (!File.Exists(path) && !File.Exists(path + NativeSaveStore.BackupSuffix)) return null;
        // The backup's own time when the primary is not there. Asking the file system for a file
        // that is missing answers 1601-01-01, which the browser would show and sort the row by.
        var timestamp = new DateTimeOffset(
            File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : File.GetLastWriteTimeUtc(path + NativeSaveStore.BackupSuffix),
            TimeSpan.Zero);
        try
        {
            var recovered = NativeSaveStore.LoadRecoveringBackup(
                path, definitions, repairPrimary: false);
            var metadata = ReadMetadata(path);
            return Summarize(
                row, metadata?.Name, timestamp, recovered.State, metadata?.Online ?? false,
                recovered.RecoveredFromBackup, recovered.PrimaryRepaired);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                           or UnauthorizedAccessException)
        {
            return Unloadable(
                row, timestamp,
                IncompatibleSave.IsIncompatible(exception)
                    ? SaveSlotStatus.Incompatible
                    : SaveSlotStatus.Unreadable);
        }
    }

    private static SaveSlotSummary Unloadable(
        int row, DateTimeOffset timestamp, SaveSlotStatus status) =>
        new(row, status == SaveSlotStatus.Incompatible ? "INCOMPATIBLE" : "UNREADABLE",
            timestamp, ScenarioId.Greed, 0, 0, "-", AiPolicyMode.Original, Status: status);

    /// <summary>The journal that travels with a slot, if the match had one to write.</summary>
    public static string JournalPath(string directory, int slot) =>
        MatchJournalStore.PathFor(SavePath(directory, slot));

    /// <summary>
    /// Writes a slot, and the match's journal beside it.
    /// </summary>
    /// <param name="journal">
    /// The recorder for this match, or null in an online one — where the authoritative journal is
    /// the server's sealed turns rather than anything this client owns.
    /// </param>
    /// <remarks>
    /// The journal is written after the save and never instead of it: a slot that saved but whose
    /// companion could not be written is a playable save with no history, which is what the slot was
    /// for. What must not happen is the pairing going stale, so a slot that gets no journal loses
    /// the one that was there — a previous game's history resumed into this one would be worse than
    /// no history at all. The display-name sidecar is treated the same way.
    /// </remarks>
    public static SaveSlotSummary Save(
        string directory,
        int slot,
        string name,
        MatchState state,
        bool online,
        MatchReplayRecorder? journal = null)
    {
        var path = SavePath(directory, slot);
        NativeSaveStore.SaveAtomic(path, state);
        var timestamp = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        var finalName = string.IsNullOrWhiteSpace(name) ? SuggestedName(state) : name.Trim();
        WriteMetadata(path, new SaveSlotMetadata(finalName, online));
        WriteJournal(path, journal);
        return Summarize(slot, finalName, timestamp, state, online);
    }

    public static MatchState Load(
        string directory, int slot, OriginalData definitions) =>
        NativeSaveStore.LoadRecoveringBackup(SavePath(directory, slot), definitions).State;

    /// <summary>
    /// The journal belonging to a slot's save, or null when there is none to continue.
    /// </summary>
    /// <param name="loaded">The state just loaded from the slot, which the journal must end at.</param>
    public static MatchReplayRecorder? LoadJournal(string directory, int slot, MatchState loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        return MatchJournalStore.TryResumeOnto(JournalPath(directory, slot), loaded);
    }

    private static void WriteJournal(string savePath, MatchReplayRecorder? journal)
    {
        var journalPath = MatchJournalStore.PathFor(savePath);
        if (journal is null)
        {
            MatchJournalStore.Delete(journalPath);
            return;
        }
        try
        {
            MatchJournalStore.SaveAtomic(journalPath, journal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                          or ArgumentOutOfRangeException
                                          or InvalidOperationException)
        {
            // The save itself is already on disk and is what the player asked for. Drop the stale
            // companion rather than leaving one that no longer describes this slot.
            //
            // InvalidOperationException is the desynced recorder: capturing a journal re-hashes the
            // match and refuses if it has moved outside the recorder. That is a real defect and one
            // worth finding, but the save has already succeeded by the time it surfaces, and a
            // player losing the game they just saved to a fault in its companion would be the worst
            // possible way to learn about it.
            MatchJournalStore.Delete(journalPath);
        }
    }

    public static string SuggestedName(MatchState state) =>
        $"{state.Setup.Scenario.ToString().ToUpperInvariant()} - TURN {state.Coordinator.Turn}";

    private static SaveSlotSummary Summarize(
        int slot,
        string? name,
        DateTimeOffset timestamp,
        MatchState state,
        bool online,
        bool recoveredFromBackup = false,
        bool primaryRepaired = false)
    {
        var humans = state.Setup.Players.Count(player => player.Controller == PlayerController.Human);
        var ai = state.Setup.Players.Count - humans;
        var matchType = online ? "ONLINE" : humans > 1 ? "HOT SEAT" : "SINGLE";
        return new SaveSlotSummary(slot,
            string.IsNullOrWhiteSpace(name) ? SuggestedName(state) : name,
            timestamp, state.Setup.Scenario, humans, ai, matchType, state.Setup.AiPolicy,
            recoveredFromBackup, primaryRepaired);
    }

    /// <summary>
    /// The slot's display name, or null when the sidecar is missing or damaged.
    /// </summary>
    /// <remarks>
    /// The sidecar is forty bytes of decoration beside a megabyte of match. A zero-length one, which
    /// is what a power cut used to leave behind, made the whole slot list as empty and refuse to
    /// load, so the save the player actually wanted was hidden by its own label.
    /// </remarks>
    private static SaveSlotMetadata? ReadMetadata(string savePath)
    {
        var path = MetadataPath(savePath);
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<SaveSlotMetadata>(File.ReadAllText(path))
                : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException
                                          or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Writes the sidecar the way every other store writes a file, and never fails a save.</summary>
    /// <returns>Whether the sidecar was written.</returns>
    private static bool WriteMetadata(string savePath, SaveSlotMetadata metadata)
    {
        var path = MetadataPath(savePath);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(
                       temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       bufferSize: 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, metadata);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            try
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Nothing to do: the save itself already succeeded.
            }
            return false;
        }
    }

    private static string MetadataPath(string savePath) => savePath + ".json";

    private static void ValidateSlot(int slot)
    {
        if (slot is < 0 or >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
    }

    private sealed record SaveSlotMetadata(string Name, bool Online);
}
