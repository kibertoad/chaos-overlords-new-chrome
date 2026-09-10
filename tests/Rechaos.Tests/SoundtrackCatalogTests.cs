using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class SoundtrackCatalogTests
{
    [Fact]
    public void ExpectedTracksPreserveGogCdAudioOrder()
    {
        Assert.Equal(
            [
                "track02.ogg", "track03.ogg", "track04.ogg", "track05.ogg",
                "track06.ogg", "track07.ogg", "track08.ogg", "track09.ogg"
            ],
            SoundtrackCatalog.ExpectedFileNames);
    }

    [Theory]
    [InlineData(OriginalSoundtrackMode.Title, new[] { "track02.ogg" })]
    [InlineData(OriginalSoundtrackMode.Gameplay, new[]
    {
        "track03.ogg", "track04.ogg", "track05.ogg",
        "track06.ogg", "track07.ogg", "track08.ogg"
    })]
    [InlineData(OriginalSoundtrackMode.Endgame, new[] { "track09.ogg" })]
    public void RecoveredModesUseExactInclusiveCdTrackRanges(
        OriginalSoundtrackMode mode, string[] expected) =>
        Assert.Equal(expected, OriginalSoundtrackPolicy.FileNamesFor(mode));

    [Theory]
    [InlineData(ClientScreen.Title, OriginalSoundtrackMode.Title)]
    [InlineData(ClientScreen.Setup, OriginalSoundtrackMode.Title)]
    [InlineData(ClientScreen.City, OriginalSoundtrackMode.Gameplay)]
    [InlineData(ClientScreen.Handoff, OriginalSoundtrackMode.Gameplay)]
    [InlineData(ClientScreen.Endgame, OriginalSoundtrackMode.Endgame)]
    public void ClientScreensSelectRecoveredMusicContext(
        ClientScreen screen, OriginalSoundtrackMode expected) =>
        Assert.Equal(expected, OriginalSoundtrackPolicy.ModeFor(screen));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 32000)]
    [InlineData(10, 64000)]
    public void RecoveredVolumeLevelsMatchOriginalStereoChannelValues(
        int level, int originalChannelValue) =>
        Assert.Equal(originalChannelValue / (float)ushort.MaxValue,
            OriginalSoundtrackPolicy.VolumeForLevel(level));

    [Fact]
    public void RecoveredVolumePolicyRejectsLevelsOutsideOptionsRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OriginalSoundtrackPolicy.VolumeForLevel(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OriginalSoundtrackPolicy.VolumeForLevel(11));
    }

    [Fact]
    public void AvailableTracksSkipMissingFilesWithoutReordering()
    {
        var root = Directory.CreateTempSubdirectory("rechaos-music-");
        try
        {
            var music = Directory.CreateDirectory(Path.Combine(root.FullName, "music"));
            File.WriteAllBytes(Path.Combine(music.FullName, "track09.ogg"), [1]);
            File.WriteAllBytes(Path.Combine(music.FullName, "track03.ogg"), [1]);

            var tracks = SoundtrackCatalog.FindAvailableTracks(root.FullName);

            Assert.Equal(
                [
                    Path.Combine(music.FullName, "track03.ogg"),
                    Path.Combine(music.FullName, "track09.ogg")
                ],
                tracks);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
