using Rechaos.Game;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class GamePreferencesStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory =
        Directory.CreateTempSubdirectory("rechaos-preferences-");

    [Fact]
    public void MissingPreferencesUseRecoveredOriginalDefault()
    {
        var preferences = GamePreferencesStore.LoadOrDefault(Path());

        Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
        Assert.Equal(OriginalSoundtrackPolicy.DefaultVolumeLevel, preferences.MusicVolumeLevel);
        Assert.Equal(AudioRouting.DefaultEffectVolumeLevel, preferences.SoundEffectVolumeLevel);
        Assert.Equal(OriginalOptionsPolicy.WarnIfIdleGangsByDefault, preferences.WarnIfIdleGangs);
        Assert.Equal(PlanningTimeLimit.None, preferences.PlanningTimeLimit);
        Assert.Equal(OriginalOptionsPolicy.ShowBaseStatisticsByDefault, preferences.ShowBaseStatistics);
        Assert.Equal(OriginalOptionsPolicy.DetailedCombatByDefault, preferences.DetailedCombat);
        Assert.Equal(OriginalOptionsPolicy.SlidePanelsByDefault, preferences.SlidePanels);
        Assert.Equal(OriginalOptionsPolicy.FullscreenByDefault, preferences.Fullscreen);
        Assert.Equal(OriginalOptionsPolicy.SmoothEventSiteImagesByDefault,
            preferences.SmoothEventSiteImages);
        Assert.Equal(GamePreferences.IntroMoviesSeenByDefault, preferences.IntroMoviesSeen);
        Assert.Equal(OriginalOptionsPolicy.AiPolicyByDefault, preferences.DefaultAiPolicy);
        Assert.Equal(OnlineServiceMode.Central, preferences.OnlineService);
        Assert.Equal(GamePreferences.DefaultCustomMultiplayerServer,
            preferences.CustomMultiplayerServer);
        Assert.Equal(OnlineLobbyPresentation.Modern, preferences.LobbyPresentation);
        Assert.Equal(OriginalOptionsPolicy.IntroOnlyOnceByDefault, preferences.IntroOnlyOnce);
        // RULE-SETUP-002: Greed when nothing is stored.
        Assert.Equal(ScenarioId.Greed, preferences.PreferredScenario);
    }

    [Fact]
    public void CurrentPreferencesRoundTrip()
    {
        var expected = new GamePreferences(
            GamePreferences.CurrentFormatVersion, 8, 3, false,
            PlanningTimeLimit.TwoMinutes, true, false, false, true, true, true,
            AiPolicyMode.Advanced, OnlineServiceMode.Custom, "https://games.example.test",
            OnlineLobbyPresentation.Classic, IntroOnlyOnce: false,
            PreferredScenario: ScenarioId.Siege);

        Assert.True(GamePreferencesStore.TrySave(Path(), expected));

        Assert.Equal(expected, GamePreferencesStore.LoadOrDefault(Path()));
    }

    [Fact]
    public void VersionFourPreferencesMigrateWithoutLosingExistingChoices()
    {
        File.WriteAllText(Path(), """
            {"FormatVersion":4,"MusicVolumeLevel":8,"SoundEffectVolumeLevel":3,
             "WarnIfIdleGangs":false,"PlanningTimeLimit":2}
            """);

        var preferences = GamePreferencesStore.LoadOrDefault(Path());

        Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
        Assert.Equal(8, preferences.MusicVolumeLevel);
        Assert.Equal(3, preferences.SoundEffectVolumeLevel);
        Assert.False(preferences.WarnIfIdleGangs);
        Assert.Equal(PlanningTimeLimit.TwoMinutes, preferences.PlanningTimeLimit);
        Assert.Equal(OriginalOptionsPolicy.ShowBaseStatisticsByDefault, preferences.ShowBaseStatistics);
        Assert.Equal(OriginalOptionsPolicy.DetailedCombatByDefault, preferences.DetailedCombat);
        Assert.Equal(OriginalOptionsPolicy.SlidePanelsByDefault, preferences.SlidePanels);
        Assert.Equal(OriginalOptionsPolicy.FullscreenByDefault, preferences.Fullscreen);
    }

    [Fact]
    public void VersionFivePreferencesMigrateWithWindowedDisplayDefault()
    {
        File.WriteAllText(Path(), """
            {"FormatVersion":5,"MusicVolumeLevel":8,"SoundEffectVolumeLevel":3,
             "WarnIfIdleGangs":false,"PlanningTimeLimit":2,
             "ShowBaseStatistics":true,"DetailedCombat":false,"SlidePanels":false}
            """);

        var preferences = GamePreferencesStore.LoadOrDefault(Path());

        Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
        Assert.Equal(8, preferences.MusicVolumeLevel);
        Assert.Equal(3, preferences.SoundEffectVolumeLevel);
        Assert.False(preferences.WarnIfIdleGangs);
        Assert.Equal(PlanningTimeLimit.TwoMinutes, preferences.PlanningTimeLimit);
        Assert.True(preferences.ShowBaseStatistics);
        Assert.False(preferences.DetailedCombat);
        Assert.False(preferences.SlidePanels);
        Assert.False(preferences.Fullscreen);
        Assert.False(preferences.SmoothEventSiteImages);
    }

    [Fact]
    public void VersionSixPreferencesMigrateWithOriginalEventImageFiltering()
    {
        File.WriteAllText(Path(), """
            {"FormatVersion":6,"MusicVolumeLevel":8,"SoundEffectVolumeLevel":3,
             "WarnIfIdleGangs":false,"PlanningTimeLimit":2,
             "ShowBaseStatistics":true,"DetailedCombat":false,"SlidePanels":false,
             "Fullscreen":true}
            """);

        var preferences = GamePreferencesStore.LoadOrDefault(Path());

        Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
        Assert.True(preferences.Fullscreen);
        Assert.False(preferences.SmoothEventSiteImages);
        Assert.False(preferences.IntroMoviesSeen);
    }

    [Fact]
    public void VersionSevenPreferencesMigrateWithTheIntroStillOwedOnce()
    {
        File.WriteAllText(Path(), """
            {"FormatVersion":7,"MusicVolumeLevel":8,"SoundEffectVolumeLevel":3,
             "WarnIfIdleGangs":false,"PlanningTimeLimit":2,
             "ShowBaseStatistics":true,"DetailedCombat":false,"SlidePanels":false,
             "Fullscreen":true,"SmoothEventSiteImages":true}
            """);

        var preferences = GamePreferencesStore.LoadOrDefault(Path());

        Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
        Assert.Equal(8, preferences.MusicVolumeLevel);
        Assert.Equal(3, preferences.SoundEffectVolumeLevel);
        Assert.Equal(PlanningTimeLimit.TwoMinutes, preferences.PlanningTimeLimit);
        Assert.True(preferences.Fullscreen);
        Assert.True(preferences.SmoothEventSiteImages);
        Assert.False(preferences.IntroMoviesSeen);
        Assert.Equal(AiPolicyMode.Original, preferences.DefaultAiPolicy);
    }

    [Fact]
    public void VersionEightPreferencesMigrateWithOriginalAiAsTheNewMatchDefault()
    {
        File.WriteAllText(Path(), """
            {"FormatVersion":8,"MusicVolumeLevel":8,"SoundEffectVolumeLevel":3,
             "WarnIfIdleGangs":false,"PlanningTimeLimit":2,
             "ShowBaseStatistics":true,"DetailedCombat":false,"SlidePanels":true,
             "Fullscreen":true,"SmoothEventSiteImages":true,"IntroMoviesSeen":true}
            """);

        var preferences = GamePreferencesStore.LoadOrDefault(Path());

        Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
        Assert.True(preferences.SlidePanels);
        Assert.True(preferences.SmoothEventSiteImages);
        Assert.True(preferences.IntroMoviesSeen);
        Assert.Equal(AiPolicyMode.Original, preferences.DefaultAiPolicy);
    }

    [Fact]
    public void VersionNinePreferencesMigrateToTheCentralService()
    {
        File.WriteAllText(Path(), """
            {"FormatVersion":9,"MusicVolumeLevel":8,"SoundEffectVolumeLevel":3,
             "WarnIfIdleGangs":false,"PlanningTimeLimit":2,
             "ShowBaseStatistics":true,"DetailedCombat":false,"SlidePanels":true,
             "Fullscreen":true,"SmoothEventSiteImages":true,"IntroMoviesSeen":true,
             "DefaultAiPolicy":1}
            """);

        var preferences = GamePreferencesStore.LoadOrDefault(Path());

        Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
        Assert.Equal(OnlineServiceMode.Central, preferences.OnlineService);
        Assert.Equal(GamePreferences.DefaultCustomMultiplayerServer,
            preferences.CustomMultiplayerServer);
        Assert.Equal(AiPolicyMode.Advanced, preferences.DefaultAiPolicy);
    }

    [Fact]
    public void VersionTenPreferencesMigrateToTheModernLobbyPresentation()
    {
        File.WriteAllText(Path(), """
            {"FormatVersion":10,"MusicVolumeLevel":8,"SoundEffectVolumeLevel":3,
             "WarnIfIdleGangs":false,"PlanningTimeLimit":2,
             "ShowBaseStatistics":true,"DetailedCombat":false,"SlidePanels":true,
             "Fullscreen":true,"SmoothEventSiteImages":true,"IntroMoviesSeen":true,
             "DefaultAiPolicy":1,"OnlineService":1,
             "CustomMultiplayerServer":"https://games.example.test"}
            """);

        var preferences = GamePreferencesStore.LoadOrDefault(Path());

        Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
        Assert.Equal(OnlineServiceMode.Custom, preferences.OnlineService);
        Assert.Equal("https://games.example.test", preferences.CustomMultiplayerServer);
        Assert.Equal(OnlineLobbyPresentation.Modern, preferences.LobbyPresentation);
    }

    // DEV-VIDEO-003: a file from before the option takes its default, on.
    [Fact]
    public void VersionElevenPreferencesMigrateWithIntroOnlyOnceOn()
    {
        File.WriteAllText(Path(), """
            {"FormatVersion":11,"MusicVolumeLevel":8,"SoundEffectVolumeLevel":3,
             "WarnIfIdleGangs":false,"PlanningTimeLimit":2,
             "ShowBaseStatistics":true,"DetailedCombat":false,"SlidePanels":true,
             "Fullscreen":true,"SmoothEventSiteImages":true,"IntroMoviesSeen":true,
             "DefaultAiPolicy":1,"OnlineService":1,
             "CustomMultiplayerServer":"https://games.example.test","LobbyPresentation":1}
            """);

        var preferences = GamePreferencesStore.LoadOrDefault(Path());

        Assert.Equal(GamePreferences.CurrentFormatVersion, preferences.FormatVersion);
        Assert.Equal(OnlineLobbyPresentation.Classic, preferences.LobbyPresentation);
        Assert.True(preferences.IntroMoviesSeen);
        Assert.True(preferences.IntroOnlyOnce);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"FormatVersion\":1,\"MusicVolumeLevel\":8}")]
    [InlineData("{\"FormatVersion\":2,\"MusicVolumeLevel\":8,\"SoundEffectVolumeLevel\":5}")]
    [InlineData("{\"FormatVersion\":3,\"MusicVolumeLevel\":8,\"SoundEffectVolumeLevel\":5}")]
    [InlineData("{\"FormatVersion\":5,\"MusicVolumeLevel\":11,\"SoundEffectVolumeLevel\":5}")]
    [InlineData("{\"FormatVersion\":5,\"MusicVolumeLevel\":5,\"SoundEffectVolumeLevel\":11}")]
    [InlineData("{\"FormatVersion\":5,\"MusicVolumeLevel\":5,\"SoundEffectVolumeLevel\":6,\"WarnIfIdleGangs\":true}")]
    [InlineData("{\"FormatVersion\":5,\"MusicVolumeLevel\":5,\"SoundEffectVolumeLevel\":6,\"WarnIfIdleGangs\":true,\"PlanningTimeLimit\":9}")]
    public void CorruptOrUnsupportedPreferencesUseDefault(string contents)
    {
        File.WriteAllText(Path(), contents);

        Assert.Equal(GamePreferences.Default, GamePreferencesStore.LoadOrDefault(Path()));
    }

    [Fact]
    public void InvalidPreferencesAreNotWritten()
    {
        Assert.False(GamePreferencesStore.TrySave(
            Path(), new GamePreferences(
                GamePreferences.CurrentFormatVersion, -1, 5, true,
                PlanningTimeLimit.None, false, true, false, false, false, false,
                AiPolicyMode.Original, OnlineServiceMode.Central,
                GamePreferences.DefaultCustomMultiplayerServer)));
        Assert.False(File.Exists(Path()));
    }

    [Fact]
    public void InvalidAiPolicyIsNotWritten()
    {
        Assert.False(GamePreferencesStore.TrySave(
            Path(), new GamePreferences(
                GamePreferences.CurrentFormatVersion, 5, 5, true,
                PlanningTimeLimit.None, false, true, false, false, false, false,
                (AiPolicyMode)99, OnlineServiceMode.Central,
                GamePreferences.DefaultCustomMultiplayerServer)));
        Assert.False(File.Exists(Path()));
    }

    [Theory]
    [InlineData(OnlineServiceMode.Custom, "not-a-url")]
    [InlineData((OnlineServiceMode)99, "https://games.example.test")]
    public void InvalidOnlineServicePreferencesAreNotWritten(
        OnlineServiceMode service,
        string server)
    {
        Assert.False(GamePreferencesStore.TrySave(
            Path(), new GamePreferences(
                GamePreferences.CurrentFormatVersion, 5, 5, true,
                PlanningTimeLimit.None, false, true, false, false, false, false,
                AiPolicyMode.Original, service, server)));
        Assert.False(File.Exists(Path()));
    }

    [Fact]
    public void InvalidOnlineLobbyPresentationIsNotWritten()
    {
        Assert.False(GamePreferencesStore.TrySave(
            Path(), new GamePreferences(
                GamePreferences.CurrentFormatVersion, 5, 5, true,
                PlanningTimeLimit.None, false, true, false, false, false, false,
                AiPolicyMode.Original, OnlineServiceMode.Central,
                GamePreferences.DefaultCustomMultiplayerServer, (OnlineLobbyPresentation)99)));
        Assert.False(File.Exists(Path()));
    }

    [Fact]
    public void InvalidPreferredScenarioIsNotWritten()
    {
        Assert.False(GamePreferencesStore.TrySave(
            Path(), GamePreferences.Default with { PreferredScenario = (ScenarioId)99 }));
        Assert.False(File.Exists(Path()));
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private string Path() => System.IO.Path.Combine(_directory.FullName, "preferences.json");
}
