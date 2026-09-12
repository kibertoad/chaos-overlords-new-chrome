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
    string MatchType)
{
    public string Details =>
        $"{Timestamp.ToLocalTime():yyyy-MM-dd HH:mm}  {Scenario.ToString().ToUpperInvariant()}  "
        + $"{MatchType}  H{HumanPlayers} A{AiPlayers}";
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
        if (!File.Exists(path)) return null;
        try
        {
            var state = NativeSaveStore.LoadRecoveringBackup(path, definitions).State;
            var timestamp = File.GetLastWriteTimeUtc(path);
            var metadata = ReadMetadata(path);
            return Summarize(slot, metadata?.Name, timestamp, state, metadata?.Online ?? false);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException
                                           or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static SaveSlotSummary Save(
        string directory, int slot, string name, MatchState state, bool online)
    {
        var path = SavePath(directory, slot);
        NativeSaveStore.SaveAtomic(path, state);
        var timestamp = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        var finalName = string.IsNullOrWhiteSpace(name) ? SuggestedName(state) : name.Trim();
        WriteMetadata(path, new SaveSlotMetadata(finalName, online));
        return Summarize(slot, finalName, timestamp, state, online);
    }

    public static MatchState Load(
        string directory, int slot, OriginalData definitions) =>
        NativeSaveStore.LoadRecoveringBackup(SavePath(directory, slot), definitions).State;

    public static string SuggestedName(MatchState state) =>
        $"{state.Setup.Scenario.ToString().ToUpperInvariant()} - TURN {state.Coordinator.Turn}";

    private static SaveSlotSummary Summarize(
        int slot, string? name, DateTime timestamp, MatchState state, bool online) =>
        Summarize(slot, name, new DateTimeOffset(timestamp, TimeSpan.Zero), state, online);

    private static SaveSlotSummary Summarize(
        int slot, string? name, DateTimeOffset timestamp, MatchState state, bool online)
    {
        var humans = state.Setup.Players.Count(player => player.Controller == PlayerController.Human);
        var ai = state.Setup.Players.Count - humans;
        var matchType = online ? "ONLINE" : humans > 1 ? "HOT SEAT" : "SINGLE";
        return new SaveSlotSummary(slot,
            string.IsNullOrWhiteSpace(name) ? SuggestedName(state) : name,
            timestamp, state.Setup.Scenario, humans, ai, matchType);
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
