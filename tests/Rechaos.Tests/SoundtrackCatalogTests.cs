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
