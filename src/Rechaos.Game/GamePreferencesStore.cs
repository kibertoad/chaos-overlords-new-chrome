using System.Text.Json;

namespace Rechaos.Game;

public static class OriginalOptionsPolicy
{
    public const bool WarnIfIdleGangsByDefault = true;
    public const bool ShowBaseStatisticsByDefault = false;
    public const bool DetailedCombatByDefault = true;
    public const bool SlidePanelsByDefault = true;
}

public sealed record GamePreferences(
    int FormatVersion,
    int MusicVolumeLevel,
    int SoundEffectVolumeLevel,
    bool WarnIfIdleGangs,
    PlanningTimeLimit PlanningTimeLimit,
    bool ShowBaseStatistics,
    bool DetailedCombat,
    bool SlidePanels)
{
    public const int CurrentFormatVersion = 5;

    public static GamePreferences Default { get; } =
        new(CurrentFormatVersion,
            OriginalSoundtrackPolicy.DefaultVolumeLevel,
            AudioRouting.DefaultEffectVolumeLevel,
            OriginalOptionsPolicy.WarnIfIdleGangsByDefault,
            PlanningTimeLimit.None,
            OriginalOptionsPolicy.ShowBaseStatisticsByDefault,
            OriginalOptionsPolicy.DetailedCombatByDefault,
            OriginalOptionsPolicy.SlidePanelsByDefault);
}

public static class GamePreferencesStore
{
    private const long MaximumFileBytes = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        RespectRequiredConstructorParameters = true
    };

    public static GamePreferences LoadOrDefault(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > MaximumFileBytes) return GamePreferences.Default;
            var bytes = File.ReadAllBytes(path);
            using var document = JsonDocument.Parse(bytes);
            if (!document.RootElement.TryGetProperty(nameof(GamePreferences.FormatVersion), out var version))
                return GamePreferences.Default;
            if (version.GetInt32() == 4)
            {
                var legacy = JsonSerializer.Deserialize<VersionFourPreferences>(bytes, JsonOptions);
                return IsValid(legacy)
                    ? new GamePreferences(GamePreferences.CurrentFormatVersion, legacy!.MusicVolumeLevel,
                        legacy.SoundEffectVolumeLevel, legacy.WarnIfIdleGangs,
                        legacy.PlanningTimeLimit,
                        OriginalOptionsPolicy.ShowBaseStatisticsByDefault,
                        OriginalOptionsPolicy.DetailedCombatByDefault,
                        OriginalOptionsPolicy.SlidePanelsByDefault)
                    : GamePreferences.Default;
            }
            var preferences = JsonSerializer.Deserialize<GamePreferences>(bytes, JsonOptions);
            return IsValid(preferences) ? preferences! : GamePreferences.Default;
        }
        catch
        {
            return GamePreferences.Default;
        }
    }

    public static bool TrySave(string path, GamePreferences preferences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(preferences);
        if (!IsValid(preferences)) return false;

        var temporaryPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using (var stream = new FileStream(
                       temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, preferences, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, path, overwrite: true);
            return true;
        }
        catch
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
                // A preference write failure must never interrupt gameplay.
            }
            return false;
        }
    }

    private static bool IsValid(GamePreferences? preferences) =>
        preferences is
        {
            FormatVersion: GamePreferences.CurrentFormatVersion,
            MusicVolumeLevel: >= OriginalSoundtrackPolicy.MinimumVolumeLevel
                and <= OriginalSoundtrackPolicy.MaximumVolumeLevel,
            SoundEffectVolumeLevel: >= AudioRouting.MinimumEffectVolumeLevel
                and <= AudioRouting.MaximumEffectVolumeLevel
        } && Enum.IsDefined(preferences.PlanningTimeLimit);

    private static bool IsValid(VersionFourPreferences? preferences) =>
        preferences is
        {
            FormatVersion: 4,
            MusicVolumeLevel: >= OriginalSoundtrackPolicy.MinimumVolumeLevel
                and <= OriginalSoundtrackPolicy.MaximumVolumeLevel,
            SoundEffectVolumeLevel: >= AudioRouting.MinimumEffectVolumeLevel
                and <= AudioRouting.MaximumEffectVolumeLevel
        } && Enum.IsDefined(preferences.PlanningTimeLimit);

    private sealed record VersionFourPreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit);
}
