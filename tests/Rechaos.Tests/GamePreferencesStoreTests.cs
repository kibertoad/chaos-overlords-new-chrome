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
        Assert.True(preferences.WarnIfIdleGangs);
        Assert.Equal(PlanningTimeLimit.None, preferences.PlanningTimeLimit);
        Assert.False(preferences.ShowBaseStatistics);
        Assert.True(preferences.DetailedCombat);
        Assert.True(preferences.SlidePanels);
    }

    [Fact]
    public void CurrentPreferencesRoundTrip()
    {
        var expected = new GamePreferences(
            GamePreferences.CurrentFormatVersion, 8, 3, false,
            PlanningTimeLimit.TwoMinutes, true, false, false);

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
        Assert.False(preferences.ShowBaseStatistics);
        Assert.True(preferences.DetailedCombat);
        Assert.True(preferences.SlidePanels);
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
                PlanningTimeLimit.None, false, true, true)));
        Assert.False(File.Exists(Path()));
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private string Path() => System.IO.Path.Combine(_directory.FullName, "preferences.json");
}
