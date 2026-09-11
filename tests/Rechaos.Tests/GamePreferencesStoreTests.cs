using Rechaos.Game;
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
    }

    [Fact]
    public void CurrentPreferencesRoundTrip()
    {
        var expected = new GamePreferences(
            GamePreferences.CurrentFormatVersion, 8, 3, false,
            PlanningTimeLimit.TwoMinutes, true, false, false, true);

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
                PlanningTimeLimit.None, false, true, true, false)));
        Assert.False(File.Exists(Path()));
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private string Path() => System.IO.Path.Combine(_directory.FullName, "preferences.json");
}
