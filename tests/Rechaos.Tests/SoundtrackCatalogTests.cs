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
    // RULE-AUDIO-001, FND-AUDIO-015: the selector uses these inclusive CD programs.
    public void RecoveredModesUseExactInclusiveCdTrackRanges(
        OriginalSoundtrackMode mode, string[] expected) =>
        Assert.Equal(expected, OriginalSoundtrackPolicy.FileNamesFor(mode));

    [Theory]
    [InlineData(ClientScreen.Title, OriginalSoundtrackMode.Title)]
    [InlineData(ClientScreen.Setup, OriginalSoundtrackMode.Title)]
    [InlineData(ClientScreen.Online, OriginalSoundtrackMode.Title)]
    [InlineData(ClientScreen.Lobby, OriginalSoundtrackMode.Title)]
    [InlineData(ClientScreen.City, OriginalSoundtrackMode.Gameplay)]
    [InlineData(ClientScreen.Handoff, OriginalSoundtrackMode.Gameplay)]
    [InlineData(ClientScreen.Endgame, OriginalSoundtrackMode.Endgame)]
    [InlineData(ClientScreen.Elimination, OriginalSoundtrackMode.Endgame)]
    public void ClientScreensSelectRecoveredMusicContext(
        ClientScreen screen, OriginalSoundtrackMode expected) =>
        Assert.Equal(expected, OriginalSoundtrackPolicy.ModeFor(screen));

    // RULE-OBJECTIVE-005: after the card of the last local human in slot order the endgame music
    // keeps playing over the planning screens; the title still has its own.
    [Theory]
    [InlineData(ClientScreen.City, OriginalSoundtrackMode.Endgame)]
    [InlineData(ClientScreen.Handoff, OriginalSoundtrackMode.Endgame)]
    [InlineData(ClientScreen.Title, OriginalSoundtrackMode.Title)]
    public void EliminationCardHoldsTheEndgameMusic(
        ClientScreen screen, OriginalSoundtrackMode expected) =>
        Assert.Equal(expected, OriginalSoundtrackPolicy.ModeFor(screen, eliminationMusicHeld: true));

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

    // RULE-AUDIO-001, FND-AUDIO-015: the two endgame entries request mode 1, and returns to
    // the title loop after a game request mode 0; cancelled network/load preparation, Options, Help, setup
    // navigation and game panels do not call the selector.
    [Theory]
    [InlineData(ClientScreen.Handoff, ClientScreen.Elimination, true)]
    [InlineData(ClientScreen.Elimination, ClientScreen.Endgame, true)]
    [InlineData(ClientScreen.City, ClientScreen.Endgame, true)]
    [InlineData(ClientScreen.Options, ClientScreen.Endgame, false)]
    [InlineData(ClientScreen.Help, ClientScreen.Elimination, false)]
    [InlineData(ClientScreen.Endgame, ClientScreen.Endgame, false)]
    [InlineData(ClientScreen.Title, ClientScreen.Setup, false)]
    [InlineData(ClientScreen.Setup, ClientScreen.Title, false)]
    [InlineData(ClientScreen.Title, ClientScreen.Title, false)]
    [InlineData(ClientScreen.Options, ClientScreen.Title, false)]
    [InlineData(ClientScreen.Help, ClientScreen.Title, false)]
    [InlineData(ClientScreen.City, ClientScreen.Title, true)]
    [InlineData(ClientScreen.Endgame, ClientScreen.Title, true)]
    [InlineData(ClientScreen.Elimination, ClientScreen.Title, true)]
    [InlineData(ClientScreen.City, ClientScreen.Gang, false)]
    [InlineData(ClientScreen.Title, ClientScreen.Online, false)]
    [InlineData(ClientScreen.Online, ClientScreen.Lobby, false)]
    [InlineData(ClientScreen.Lobby, ClientScreen.Setup, false)]
    [InlineData(ClientScreen.Setup, ClientScreen.Lobby, false)]
    [InlineData(ClientScreen.Online, ClientScreen.Title, false)]
    [InlineData(ClientScreen.Lobby, ClientScreen.Title, false)]
    public void OriginalSelectorEntriesRestartEvenWhenTheirMusicModeIsAlreadySelected(
        ClientScreen previous, ClientScreen current, bool restart) =>
        Assert.Equal(restart, OriginalSoundtrackPolicy.RestartsOnEntry(previous, current));

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
