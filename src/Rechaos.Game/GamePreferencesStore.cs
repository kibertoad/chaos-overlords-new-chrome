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

    /// <summary>DEV-VIDEO-003: the original plays the intro at every start.</summary>
    public const bool IntroOnlyOnceByDefault = false;

    /// <summary>DEV-EQUIP-001, DEV-AI-001: a new match follows the original's rules.</summary>
    public const bool RevisedRulesByDefault = false;
}

/// <summary>Which coordination service the Online screen uses.</summary>
public enum OnlineServiceMode
{
    Central,
    Custom
}

/// <summary>The local presentation used for an otherwise identical online lobby.</summary>
public enum OnlineLobbyPresentation
{
    Modern,
    Classic
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
    string CustomMultiplayerServer,
    OnlineLobbyPresentation LobbyPresentation = OnlineLobbyPresentation.Modern,
    bool IntroOnlyOnce = OriginalOptionsPolicy.IntroOnlyOnceByDefault,
    bool RevisedRules = OriginalOptionsPolicy.RevisedRulesByDefault)
{
    public const int CurrentFormatVersion = 12;
    public const string DefaultCustomMultiplayerServer = "http://localhost:8787";

    /// <summary>Preferences that have never recorded a showing leave the intro owed, so the
    /// first run streams it even with Intro only once switched on.</summary>
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
            var formatVersion = version.GetInt32();
            VersionFourPreferences? legacy = formatVersion switch
            {
                4 => Read<VersionFourPreferences>(bytes),
                5 => Read<VersionFivePreferences>(bytes),
                6 => Read<VersionSixPreferences>(bytes),
                7 => Read<VersionSevenPreferences>(bytes),
                8 => Read<VersionEightPreferences>(bytes),
                9 => Read<VersionNinePreferences>(bytes),
                10 => Read<VersionTenPreferences>(bytes),
                11 => Read<VersionElevenPreferences>(bytes),
                _ => null
            };
            if (formatVersion is >= 4 and <= 11)
            {
                return legacy is not null && legacy.IsValid(formatVersion)
                    ? legacy.Upgrade()
                    : GamePreferences.Default;
            }
            var preferences = Read<GamePreferences>(bytes);
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

    private static T? Read<T>(byte[] bytes) => JsonSerializer.Deserialize<T>(bytes, JsonOptions);

    private static bool IsValid(GamePreferences? preferences) =>
        preferences is { FormatVersion: GamePreferences.CurrentFormatVersion }
        && LevelsAreValid(
            preferences.MusicVolumeLevel,
            preferences.SoundEffectVolumeLevel,
            preferences.PlanningTimeLimit)
        && Enum.IsDefined(preferences.DefaultAiPolicy)
        && Enum.IsDefined(preferences.OnlineService)
        && Enum.IsDefined(preferences.LobbyPresentation)
        && IsServerAddress(preferences.CustomMultiplayerServer);

    private static bool LevelsAreValid(int music, int effects, PlanningTimeLimit limit) =>
        music is >= OriginalSoundtrackPolicy.MinimumVolumeLevel
            and <= OriginalSoundtrackPolicy.MaximumVolumeLevel
        && effects is >= AudioRouting.MinimumEffectVolumeLevel
            and <= AudioRouting.MaximumEffectVolumeLevel
        && Enum.IsDefined(limit);

    private static bool IsServerAddress(string address) =>
        !string.IsNullOrWhiteSpace(address)
        && address.Length <= 96
        && Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    // Each older format is the one before it plus the fields its version added, so a record states
    // only what is new: how to check it, and how to carry it into the current preferences. A field
    // no version of the file stored keeps the value a fresh install has.
    private record VersionFourPreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit)
    {
        public virtual bool IsValid(int formatVersion) =>
            FormatVersion == formatVersion
            && LevelsAreValid(MusicVolumeLevel, SoundEffectVolumeLevel, PlanningTimeLimit);

        public virtual GamePreferences Upgrade() => GamePreferences.Default with
        {
            MusicVolumeLevel = MusicVolumeLevel,
            SoundEffectVolumeLevel = SoundEffectVolumeLevel,
            WarnIfIdleGangs = WarnIfIdleGangs,
            PlanningTimeLimit = PlanningTimeLimit
        };
    }

    private record VersionFivePreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit,
        bool ShowBaseStatistics,
        bool DetailedCombat,
        bool SlidePanels)
        : VersionFourPreferences(
            FormatVersion, MusicVolumeLevel, SoundEffectVolumeLevel, WarnIfIdleGangs, PlanningTimeLimit)
    {
        public override GamePreferences Upgrade() => base.Upgrade() with
        {
            ShowBaseStatistics = ShowBaseStatistics,
            DetailedCombat = DetailedCombat,
            SlidePanels = SlidePanels
        };
    }

    private record VersionSixPreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit,
        bool ShowBaseStatistics,
        bool DetailedCombat,
        bool SlidePanels,
        bool Fullscreen)
        : VersionFivePreferences(
            FormatVersion, MusicVolumeLevel, SoundEffectVolumeLevel, WarnIfIdleGangs, PlanningTimeLimit,
            ShowBaseStatistics, DetailedCombat, SlidePanels)
    {
        public override GamePreferences Upgrade() => base.Upgrade() with { Fullscreen = Fullscreen };
    }

    private record VersionSevenPreferences(
        int FormatVersion,
        int MusicVolumeLevel,
        int SoundEffectVolumeLevel,
        bool WarnIfIdleGangs,
        PlanningTimeLimit PlanningTimeLimit,
        bool ShowBaseStatistics,
        bool DetailedCombat,
        bool SlidePanels,
        bool Fullscreen,
        bool SmoothEventSiteImages)
        : VersionSixPreferences(
            FormatVersion, MusicVolumeLevel, SoundEffectVolumeLevel, WarnIfIdleGangs, PlanningTimeLimit,
            ShowBaseStatistics, DetailedCombat, SlidePanels, Fullscreen)
    {
        public override GamePreferences Upgrade() =>
            base.Upgrade() with { SmoothEventSiteImages = SmoothEventSiteImages };
    }

    private record VersionEightPreferences(
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
        bool IntroMoviesSeen)
        : VersionSevenPreferences(
            FormatVersion, MusicVolumeLevel, SoundEffectVolumeLevel, WarnIfIdleGangs, PlanningTimeLimit,
            ShowBaseStatistics, DetailedCombat, SlidePanels, Fullscreen, SmoothEventSiteImages)
    {
        public override GamePreferences Upgrade() =>
            base.Upgrade() with { IntroMoviesSeen = IntroMoviesSeen };
    }

    private record VersionNinePreferences(
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
        AiPolicyMode DefaultAiPolicy)
        : VersionEightPreferences(
            FormatVersion, MusicVolumeLevel, SoundEffectVolumeLevel, WarnIfIdleGangs, PlanningTimeLimit,
            ShowBaseStatistics, DetailedCombat, SlidePanels, Fullscreen, SmoothEventSiteImages,
            IntroMoviesSeen)
    {
        public override bool IsValid(int formatVersion) =>
            base.IsValid(formatVersion) && Enum.IsDefined(DefaultAiPolicy);

        public override GamePreferences Upgrade() =>
            base.Upgrade() with { DefaultAiPolicy = DefaultAiPolicy };
    }

    private record VersionTenPreferences(
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
        : VersionNinePreferences(
            FormatVersion, MusicVolumeLevel, SoundEffectVolumeLevel, WarnIfIdleGangs, PlanningTimeLimit,
            ShowBaseStatistics, DetailedCombat, SlidePanels, Fullscreen, SmoothEventSiteImages,
            IntroMoviesSeen, DefaultAiPolicy)
    {
        public override bool IsValid(int formatVersion) =>
            base.IsValid(formatVersion)
            && Enum.IsDefined(OnlineService)
            && IsServerAddress(CustomMultiplayerServer);

        public override GamePreferences Upgrade() => base.Upgrade() with
        {
            OnlineService = OnlineService,
            CustomMultiplayerServer = CustomMultiplayerServer
        };
    }

    private sealed record VersionElevenPreferences(
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
        string CustomMultiplayerServer,
        OnlineLobbyPresentation LobbyPresentation)
        : VersionTenPreferences(
            FormatVersion, MusicVolumeLevel, SoundEffectVolumeLevel, WarnIfIdleGangs, PlanningTimeLimit,
            ShowBaseStatistics, DetailedCombat, SlidePanels, Fullscreen, SmoothEventSiteImages,
            IntroMoviesSeen, DefaultAiPolicy, OnlineService, CustomMultiplayerServer)
    {
        public override bool IsValid(int formatVersion) =>
            base.IsValid(formatVersion) && Enum.IsDefined(LobbyPresentation);

        public override GamePreferences Upgrade() =>
            base.Upgrade() with { LobbyPresentation = LobbyPresentation };
    }
}
