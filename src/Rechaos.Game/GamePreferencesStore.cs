using System.Text.Json;

namespace Rechaos.Game;

public sealed record GamePreferences(
    int FormatVersion,
    int MusicVolumeLevel,
    int SoundEffectVolumeLevel)
{
    public const int CurrentFormatVersion = 2;

    public static GamePreferences Default { get; } =
        new(CurrentFormatVersion,
            OriginalSoundtrackPolicy.DefaultVolumeLevel,
            AudioRouting.DefaultEffectVolumeLevel);
}

public static class GamePreferencesStore
{
    private const long MaximumFileBytes = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static GamePreferences LoadOrDefault(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > MaximumFileBytes) return GamePreferences.Default;
            using var stream = file.OpenRead();
            var preferences = JsonSerializer.Deserialize<GamePreferences>(stream, JsonOptions);
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
        };
}
