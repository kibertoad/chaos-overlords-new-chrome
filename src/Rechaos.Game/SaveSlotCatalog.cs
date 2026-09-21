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

    /// <remarks>
    /// Every question about the two files is answered from one stat each, taken up front:
    /// <see cref="FileInfo"/> caches what it read, so existence, timestamp and the sidecar's
    /// staleness check cannot contradict each other, and none of them throws when a file is
    /// deleted underneath the listing. Asking the file system a second time could, and a
    /// <see cref="FileNotFoundException"/> from the staleness check escaped this method entirely
    /// — past the catch that degrades a row to <see cref="SaveSlotStatus.Unreadable"/>, and out
    /// through the browser into the game loop.
    /// </remarks>
    private static SaveSlotSummary? ReadFile(string path, int row, OriginalData definitions)
    {
        var primary = new FileInfo(path);
        var backup = new FileInfo(path + NativeSaveStore.BackupSuffix);
        if (!primary.Exists && !backup.Exists) return null;
        // The backup's own time when the primary is not there. Asking the file system for a file
        // that is missing answers 1601-01-01, which the browser would show and sort the row by.
        var timestamp = new DateTimeOffset(
            (primary.Exists ? primary : backup).LastWriteTimeUtc, TimeSpan.Zero);
        // A current sidecar this build wrote is all the browser needs. Do not deserialize a
        // multi-megabyte match merely to draw its row; the full validation happens when the player
        // chooses to load it.
        var metadata = ReadMetadata(path);
        if (primary.Exists && metadata is not null
            && metadata.Matches(primary)
            && metadata.WrittenByThisBuild(definitions)
            && metadata.ToSummary(row, timestamp) is { } summary)
            return summary;
        try
        {
            var recovered = NativeSaveStore.LoadRecoveringBackup(
                path, definitions, repairPrimary: false);
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
        var summary = Summarize(slot, finalName, timestamp, state, online);
        WriteMetadata(path, summary, state.Definitions);
        WriteJournal(path, journal);
        return summary;
    }

    /// <summary>
    /// The rolling autosave's browser row for the turn being captured.
    /// </summary>
    /// <remarks>
    /// Read on the game thread, where the match is; the worker that writes the file is handed the
    /// result along with the bytes. The timestamp here is a placeholder the sidecar never keeps:
    /// what it records, and what the browser draws, is the write time of the file itself.
    /// </remarks>
    public static SaveSlotSummary DescribeAutoSave(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Summarize(
            AutoSaveRow, "AUTOSAVE", DateTimeOffset.UnixEpoch, state, online: false);
    }

    /// <summary>Writes the rolling autosave's browser sidecar after its primary is durable.</summary>
    /// <param name="row">The row captured by <see cref="DescribeAutoSave"/> for these bytes.</param>
    public static void WriteAutoSaveMetadata(
        string path, SaveSlotSummary row, OriginalData definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(definitions);
        WriteMetadata(path, row, definitions);
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
    /// The sidecar is small browser data beside a megabyte of match. A zero-length one, which
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
    /// <remarks>
    /// The sidecar is built inside the guard rather than handed in ready-made: describing the save
    /// means asking the file system for its length and write time, which throws when the save is
    /// no longer there. A save already on disk must not be reported as failed — leaving the
    /// previous match's journal paired with it — because its small companion could not be
    /// described.
    /// </remarks>
    private static bool WriteMetadata(
        string savePath, SaveSlotSummary summary, OriginalData definitions)
    {
        var path = MetadataPath(savePath);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            if (SaveSlotMetadata.From(summary, savePath, definitions) is not { } metadata)
                return false;
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

    /// <param name="SaveLength">The save's length when this was written, for the staleness check.</param>
    /// <param name="SaveWriteUtcTicks">The save's write time when this was written.</param>
    /// <param name="FormatVersion">The save format the writing build spoke.</param>
    /// <param name="DefinitionsSha256">The bundled definitions the writing build played with.</param>
    /// <remarks>
    /// Every member is nullable and unmapped ones are ignored, so a sidecar from a build that
    /// wrote fewer fields still deserializes and simply fails the checks below, which costs a full
    /// load of that one slot and nothing else.
    /// </remarks>
    private sealed record SaveSlotMetadata(
        string? Name,
        bool? Online,
        long? SaveLength,
        long? SaveWriteUtcTicks,
        ScenarioId? Scenario,
        int? HumanPlayers,
        int? AiPlayers,
        string? MatchType,
        AiPolicyMode? AiPolicy,
        int? FormatVersion = null,
        string? DefinitionsSha256 = null)
    {
        /// <summary>The sidecar for a save, or null when the save is no longer there to describe.</summary>
        public static SaveSlotMetadata? From(
            SaveSlotSummary summary, string path, OriginalData definitions)
        {
            var file = new FileInfo(path);
            return file.Exists
                ? new SaveSlotMetadata(
                    summary.Name, summary.MatchType == "ONLINE", file.Length,
                    file.LastWriteTimeUtc.Ticks, summary.Scenario, summary.HumanPlayers,
                    summary.AiPlayers, summary.MatchType, summary.AiPolicy,
                    NativeSaveSerializer.CurrentFormatVersion,
                    NativeSaveSerializer.DefinitionsFingerprint(definitions))
                : null;
        }

        /// <summary>Whether the save still is the one this sidecar was written for.</summary>
        public bool Matches(FileInfo file) => SaveLength == file.Length
                                              && SaveWriteUtcTicks == file.LastWriteTimeUtc.Ticks;

        /// <summary>
        /// Whether the save this sidecar describes is one this build could load.
        /// </summary>
        /// <remarks>
        /// <see cref="Matches"/> proves only that the file has not been edited since; it says
        /// nothing about the build that wrote it. A save from a newer format version, or written
        /// against other bundled definitions, is byte-identical on disk and its sidecar is as
        /// current as any other, so without this the browser drew an unloadable slot as a playable
        /// row with real-looking match details: the cursor parked on it, and choosing it reported a
        /// generic failure rather than <see cref="SaveSlotStatus.Incompatible"/>. Anything this
        /// build did not write falls back to the full load, which is what classifies it.
        /// </remarks>
        public bool WrittenByThisBuild(OriginalData definitions) =>
            FormatVersion == NativeSaveSerializer.CurrentFormatVersion
            && DefinitionsSha256 is { Length: > 0 } fingerprint
            && string.Equals(
                fingerprint, NativeSaveSerializer.DefinitionsFingerprint(definitions),
                StringComparison.OrdinalIgnoreCase);

        public SaveSlotSummary? ToSummary(int row, DateTimeOffset timestamp) =>
            Name is { Length: > 0 } name && Online is { } online && Scenario is { } scenario
            && HumanPlayers is { } humans && AiPlayers is { } ai && MatchType is { Length: > 0 } matchType
            && AiPolicy is { } aiPolicy
                ? new SaveSlotSummary(row, name, timestamp, scenario, humans, ai, matchType, aiPolicy)
                : null;
    }
}
