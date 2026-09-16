using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;

namespace Rechaos.Game;

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
    bool PrimaryRepaired = false)
{
    public string Details =>
        $"{Timestamp.ToLocalTime():yyyy-MM-dd HH:mm}  {Scenario.ToString().ToUpperInvariant()}  "
        + $"{MatchType}  H{HumanPlayers} A{AiPlayers}  {AiPolicyPresentation.Label(AiPolicy)} AI"
        + (RecoveredFromBackup
            ? PrimaryRepaired ? "  RECOVERED" : "  BACKUP ONLY"
            : string.Empty);
}

public static class SaveSlotCatalog
{
    public const int SlotCount = 9;

    public static string SavePath(string directory, int slot)
    {
        ValidateSlot(slot);
        return Path.Combine(directory, $"save-slot-{slot + 1}.rchsave");
    }

    public static SaveSlotSummary? Read(
        string directory, int slot, OriginalData definitions)
    {
        var path = SavePath(directory, slot);
        if (!File.Exists(path) && !File.Exists(path + NativeSaveStore.BackupSuffix)) return null;
        try
        {
            var recovered = NativeSaveStore.LoadRecoveringBackup(path, definitions);
            // The backup's own time when recovery could not put the primary back (a read-only
            // directory, a full disk). Asking the file system for a file that is not there answers
            // 1601-01-01, which the browser would then show and sort the row by.
            var timestamp = File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : File.GetLastWriteTimeUtc(path + NativeSaveStore.BackupSuffix);
            var metadata = ReadMetadata(path);
            return Summarize(
                slot, metadata?.Name, timestamp, recovered.State, metadata?.Online ?? false,
                recovered.RecoveredFromBackup, recovered.PrimaryRepaired);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                           or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

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
    /// no history at all.
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
        DateTime timestamp,
        MatchState state,
        bool online,
        bool recoveredFromBackup = false,
        bool primaryRepaired = false) =>
        Summarize(
            slot, name, new DateTimeOffset(timestamp, TimeSpan.Zero), state, online,
            recoveredFromBackup, primaryRepaired);

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

    private static SaveSlotMetadata? ReadMetadata(string savePath)
    {
        var path = MetadataPath(savePath);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<SaveSlotMetadata>(File.ReadAllText(path))
            : null;
    }

    private static void WriteMetadata(string savePath, SaveSlotMetadata metadata)
    {
        var path = MetadataPath(savePath);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(metadata));
        File.Move(temporary, path, overwrite: true);
    }

    private static string MetadataPath(string savePath) => savePath + ".json";

    private static void ValidateSlot(int slot)
    {
        if (slot is < 0 or >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
    }

    private sealed record SaveSlotMetadata(string Name, bool Online);
}
