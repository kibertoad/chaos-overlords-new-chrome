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
    }

    [Fact]
    public void CurrentPreferencesRoundTrip()
    {
        var expected = new GamePreferences(GamePreferences.CurrentFormatVersion, 8, 3);

        Assert.True(GamePreferencesStore.TrySave(Path(), expected));

        Assert.Equal(expected, GamePreferencesStore.LoadOrDefault(Path()));
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"FormatVersion\":1,\"MusicVolumeLevel\":8}")]
    [InlineData("{\"FormatVersion\":2,\"MusicVolumeLevel\":11,\"SoundEffectVolumeLevel\":5}")]
    [InlineData("{\"FormatVersion\":2,\"MusicVolumeLevel\":5,\"SoundEffectVolumeLevel\":11}")]
    public void CorruptOrUnsupportedPreferencesUseDefault(string contents)
    {
        File.WriteAllText(Path(), contents);

        Assert.Equal(GamePreferences.Default, GamePreferencesStore.LoadOrDefault(Path()));
    }

    [Fact]
    public void InvalidPreferencesAreNotWritten()
    {
        Assert.False(GamePreferencesStore.TrySave(
            Path(), new GamePreferences(GamePreferences.CurrentFormatVersion, -1, 5)));
        Assert.False(File.Exists(Path()));
    }

    public void Dispose() => _directory.Delete(recursive: true);

    private string Path() => System.IO.Path.Combine(_directory.FullName, "preferences.json");
}
