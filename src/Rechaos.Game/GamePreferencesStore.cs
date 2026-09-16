using System.Text.Json;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class OriginalOptionsPolicy
{
    public const bool WarnIfIdleGangsByDefault = true;
    public const bool ShowBaseStatisticsByDefault = false;
    public const bool DetailedCombatByDefault = true;
    public const bool SlidePanelsByDefault = false;
    public const bool FullscreenByDefault = false;
    public const bool SmoothEventSiteImagesByDefault = false;
    public const AiPolicyMode AiPolicyByDefault = AiPolicyMode.Original;
}

/// <summary>Which coordination service the Online screen uses.</summary>
public enum OnlineServiceMode
{
    Central,
    Custom
}

public sealed record GamePreferences(
    int FormatVersion,
    int MusicVolumeLevel,
    int SoundEffectVolumeLevel,
    bool WarnIfIdleGangs,
    PlanningTimeLimit PlanningTimeLimit,
    bool ShowBaseStatistics,
    bool DetailedCombat,
    bool SlidePanels,
    bool Fullscreen,
    bool SmoothEventSiteImages,
    bool IntroMoviesSeen,
    AiPolicyMode DefaultAiPolicy,
    OnlineServiceMode OnlineService,
    string CustomMultiplayerServer)
{
    public const int CurrentFormatVersion = 10;
    public const string DefaultCustomMultiplayerServer = "http://localhost:8787";

    /// <summary>Preferences that have never recorded a showing leave the intro owed, so the
    /// first run streams it; the title screen replays it on request from then on.</summary>
    public const bool IntroMoviesSeenByDefault = false;

    public static GamePreferences Default { get; } =
        new(CurrentFormatVersion,
            OriginalSoundtrackPolicy.DefaultVolumeLevel,
            AudioRouting.DefaultEffectVolumeLevel,
            OriginalOptionsPolicy.WarnIfIdleGangsByDefault,
            PlanningTimeLimit.None,
            OriginalOptionsPolicy.ShowBaseStatisticsByDefault,
            OriginalOptionsPolicy.DetailedCombatByDefault,
            OriginalOptionsPolicy.SlidePanelsByDefault,
            OriginalOptionsPolicy.FullscreenByDefault,
            OriginalOptionsPolicy.SmoothEventSiteImagesByDefault,
            IntroMoviesSeenByDefault,
            OriginalOptionsPolicy.AiPolicyByDefault,
            OnlineServiceMode.Central,
            DefaultCustomMultiplayerServer);
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
                        OriginalOptionsPolicy.SlidePanelsByDefault,
                        OriginalOptionsPolicy.FullscreenByDefault,
                        OriginalOptionsPolicy.SmoothEventSiteImagesByDefault,
                        GamePreferences.IntroMoviesSeenByDefault,
                        OriginalOptionsPolicy.AiPolicyByDefault,
                        OnlineServiceMode.Central,
                        GamePreferences.DefaultCustomMultiplayerServer)
                    : GamePreferences.Default;
            }
            if (version.GetInt32() == 5)
            {
                var legacy = JsonSerializer.Deserialize<VersionFivePreferences>(bytes, JsonOptions);
                return IsValid(legacy)
                    ? new GamePreferences(GamePreferences.CurrentFormatVersion,
                        legacy!.MusicVolumeLevel, legacy.SoundEffectVolumeLevel,
                        legacy.WarnIfIdleGangs, legacy.PlanningTimeLimit,
                        legacy.ShowBaseStatistics, legacy.DetailedCombat,
                        legacy.SlidePanels, OriginalOptionsPolicy.FullscreenByDefault,
                        OriginalOptionsPolicy.SmoothEventSiteImagesByDefault,
                        GamePreferences.IntroMoviesSeenByDefault,
                        OriginalOptionsPolicy.AiPolicyByDefault,
                        OnlineServiceMode.Central,
                        GamePreferences.DefaultCustomMultiplayerServer)
                    : GamePreferences.Default;
            }
            if (version.GetInt32() == 6)
            {
                var legacy = JsonSerializer.Deserialize<VersionSixPreferences>(bytes, JsonOptions);
                return IsValid(legacy)
                    ? new GamePreferences(GamePreferences.CurrentFormatVersion,
                        legacy!.MusicVolumeLevel, legacy.SoundEffectVolumeLevel,
                        legacy.WarnIfIdleGangs, legacy.PlanningTimeLimit,
                        legacy.ShowBaseStatistics, legacy.DetailedCombat,
                        legacy.SlidePanels, legacy.Fullscreen,
                        OriginalOptionsPolicy.SmoothEventSiteImagesByDefault,
                        GamePreferences.IntroMoviesSeenByDefault,
                        OriginalOptionsPolicy.AiPolicyByDefault,
                        OnlineServiceMode.Central,
                        GamePreferences.DefaultCustomMultiplayerServer)
                    : GamePreferences.Default;
            }
            if (version.GetInt32() == 7)
            {
                var legacy = JsonSerializer.Deserialize<VersionSevenPreferences>(bytes, JsonOptions);
                return IsValid(legacy)
                    ? new GamePreferences(GamePreferences.CurrentFormatVersion,
                        legacy!.MusicVolumeLevel, legacy.SoundEffectVolumeLevel,
                        legacy.WarnIfIdleGangs, legacy.PlanningTimeLimit,
                        legacy.ShowBaseStatistics, legacy.DetailedCombat,
                        legacy.SlidePanels, legacy.Fullscreen,
                        legacy.SmoothEventSiteImages,
                        GamePreferences.IntroMoviesSeenByDefault,
                        OriginalOptionsPolicy.AiPolicyByDefault,
                        OnlineServiceMode.Central,
                        GamePreferences.DefaultCustomMultiplayerServer)
                    : GamePreferences.Default;
            }
            if (version.GetInt32() == 8)
            {
                var legacy = JsonSerializer.Deserialize<VersionEightPreferences>(bytes, JsonOptions);
                return IsValid(legacy)
                    ? new GamePreferences(GamePreferences.CurrentFormatVersion,
                        legacy!.MusicVolumeLevel, legacy.SoundEffectVolumeLevel,
                        legacy.WarnIfIdleGangs, legacy.PlanningTimeLimit,
                        legacy.ShowBaseStatistics, legacy.DetailedCombat,
                        legacy.SlidePanels, legacy.Fullscreen,
                        legacy.SmoothEventSiteImages, legacy.IntroMoviesSeen,
                        OriginalOptionsPolicy.AiPolicyByDefault,
                        OnlineServiceMode.Central,
                        GamePreferences.DefaultCustomMultiplayerServer)
                    : GamePreferences.Default;
            }
            if (version.GetInt32() == 9)
            {
                var legacy = JsonSerializer.Deserialize<VersionNinePreferences>(bytes, JsonOptions);
                return IsValid(legacy)
                    ? new GamePreferences(GamePreferences.CurrentFormatVersion,
                        legacy!.MusicVolumeLevel, legacy.SoundEffectVolumeLevel,
                        legacy.WarnIfIdleGangs, legacy.PlanningTimeLimit,
                        legacy.ShowBaseStatistics, legacy.DetailedCombat,
                        legacy.SlidePanels, legacy.Fullscreen,
                        legacy.SmoothEventSiteImages, legacy.IntroMoviesSeen,
                        legacy.DefaultAiPolicy, OnlineServiceMode.Central,
                        GamePreferences.DefaultCustomMultiplayerServer)
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
        } && Enum.IsDefined(preferences.PlanningTimeLimit)
          && Enum.IsDefined(preferences.DefaultAiPolicy)
          && Enum.IsDefined(preferences.OnlineService)
          && IsServerAddress(preferences.CustomMultiplayerServer);

    private static bool IsValid(VersionFourPreferences? preferences) =>
        preferences is
        {
            FormatVersion: 4,
            MusicVolumeLevel: >= OriginalSoundtrackPolicy.MinimumVolumeLevel
                and <= OriginalSoundtrackPolicy.MaximumVolumeLevel,
            SoundEffectVolumeLevel: >= AudioRouting.MinimumEffectVolumeLevel
                and <= AudioRouting.MaximumEffectVolumeLevel
        } && Enum.IsDefined(preferences.PlanningTimeLimit);

    private static bool IsValid(VersionFivePreferences? preferences) =>
        preferences is
        {
            FormatVersion: 5,
            MusicVolumeLevel: >= OriginalSoundtrackPolicy.MinimumVolumeLevel
                and <= OriginalSoundtrackPolicy.MaximumVolumeLevel,
            SoundEffectVolumeLevel: >= AudioRouting.MinimumEffectVolumeLevel
                and <= AudioRouting.MaximumEffectVolumeLevel
        } && Enum.IsDefined(preferences.PlanningTimeLimit);

    private static bool IsValid(VersionSevenPreferences? preferences) =>
        preferences is
        {
            FormatVersion: 7,
            MusicVolumeLevel: >= OriginalSoundtrackPolicy.MinimumVolumeLevel
                and <= OriginalSoundtrackPolicy.MaximumVolumeLevel,
            SoundEffectVolumeLevel: >= AudioRouting.MinimumEffectVolumeLevel
                and <= AudioRouting.MaximumEffectVolumeLevel
        } && Enum.IsDefined(preferences.PlanningTimeLimit);

    private static bool IsValid(VersionSixPreferences? preferences) =>
        preferences is
        {
            FormatVersion: 6,
            MusicVolumeLevel: >= OriginalSoundtrackPolicy.MinimumVolumeLevel
                and <= OriginalSoundtrackPolicy.MaximumVolumeLevel,
            SoundEffectVolumeLevel: >= AudioRouting.MinimumEffectVolumeLevel
                and <= AudioRouting.MaximumEffectVolumeLevel
        } && Enum.IsDefined(preferences.PlanningTimeLimit);

    private static bool IsValid(VersionEightPreferences? preferences) =>
        preferences is
        {
            FormatVersion: 8,
            MusicVolumeLevel: >= OriginalSoundtrackPolicy.MinimumVolumeLevel
                and <= OriginalSoundtrackPolicy.MaximumVolumeLevel,
            SoundEffectVolumeLevel: >= AudioRouting.MinimumEffectVolumeLevel
                and <= AudioRouting.MaximumEffectVolumeLevel
        } && Enum.IsDefined(preferences.PlanningTimeLimit);

    private static bool IsValid(VersionNinePreferences? preferences) =>
        preferences is
        {
            FormatVersion: 9,
            MusicVolumeLevel: >= OriginalSoundtrackPolicy.MinimumVolumeLevel
                and <= OriginalSoundtrackPolicy.MaximumVolumeLevel,
            SoundEffectVolumeLevel: >= AudioRouting.MinimumEffectVolumeLevel
                and <= AudioRouting.MaximumEffectVolumeLevel
        } && Enum.IsDefined(preferences.PlanningTimeLimit)
          && Enum.IsDefined(preferences.DefaultAiPolicy);

    private static bool IsServerAddress(string address) =>
        !string.IsNullOrWhiteSpace(address)
        && address.Length <= 96
        && Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    private sealed record VersionFourPreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit);

    private sealed record VersionFivePreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit,
        bool ShowBaseStatistics,
        bool DetailedCombat,
        bool SlidePanels);

    private sealed record VersionSixPreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit,
        bool ShowBaseStatistics,
        bool DetailedCombat,
        bool SlidePanels,
        bool Fullscreen);

    private sealed record VersionSevenPreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit,
        bool ShowBaseStatistics,
        bool DetailedCombat,
        bool SlidePanels,
        bool Fullscreen,
        bool SmoothEventSiteImages);

    private sealed record VersionEightPreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit,
        bool ShowBaseStatistics,
        bool DetailedCombat,
        bool SlidePanels,
        bool Fullscreen,
        bool SmoothEventSiteImages,
        bool IntroMoviesSeen);

    private sealed record VersionNinePreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit,
        bool ShowBaseStatistics,
        bool DetailedCombat,
        bool SlidePanels,
        bool Fullscreen,
        bool SmoothEventSiteImages,
        bool IntroMoviesSeen,
        AiPolicyMode DefaultAiPolicy);
}
