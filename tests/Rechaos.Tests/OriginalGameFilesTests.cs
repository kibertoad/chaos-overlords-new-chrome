using Xunit;

namespace Rechaos.Tests;

public sealed class OriginalGameFilesTests : IDisposable
{
    private const string Build = "BLD-GOG-EN-1.1";
    private readonly string root = Path.Combine(Path.GetTempPath(), "rechaos-game-dir-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FindsFilesByBuildAndPath()
    {
        var hash = Write(Path.Combine(Build, "DATA", "SITES.DAT"), [1, 2, 3]);

        Assert.Equal(Path.Combine(root, Build, "DATA", "SITES.DAT"),
            OriginalGameFiles.Resolve(root, Build, "DATA/SITES.DAT", hash));
    }

    [Fact]
    public void FindsDiscFilesUnderTheDiscDirectory()
    {
        var hash = Write(Path.Combine(Build, "CD2", "MOVIES", "INTRO.SMK"), [4]);

        Assert.Equal(Path.Combine(root, Build, "CD2", "MOVIES", "INTRO.SMK"),
            OriginalGameFiles.Resolve(root, Build, "CD2:MOVIES/INTRO.SMK", hash));
    }

    [Fact]
    public void FindsCapturesByHash()
    {
        var hash = Hash([5, 6]);
        Write(Path.Combine("captures", hash), [5, 6]);

        Assert.Equal(Path.Combine(root, "captures", hash), OriginalGameFiles.ResolveCapture(root, hash));
    }

    [Fact]
    public void ReportsAbsentFilesAsMissing()
    {
        var hash = Hash([1]);

        Assert.Null(OriginalGameFiles.Resolve(null, Build, "GAME.EXE", hash));
        Assert.Null(OriginalGameFiles.Resolve(root, Build, "GAME.EXE", hash));
        Assert.Null(OriginalGameFiles.ResolveCapture(root, hash));
    }

    [Fact]
    public void RejectsFilesWhoseHashDiffersFromTheSpec()
    {
        Write(Path.Combine(Build, "GAME.EXE"), [1, 2, 3]);

        Assert.Throws<InvalidDataException>(() =>
            OriginalGameFiles.Resolve(root, Build, "GAME.EXE", Hash([9])));
    }

    [Theory]
    [InlineData("GOG-EN-1.1", "GAME.EXE")]
    [InlineData(Build, "../GAME.EXE")]
    [InlineData(Build, "DATA\\GAME.EXE")]
    [InlineData(Build, "/GAME.EXE")]
    [InlineData(Build, "DATA//GAME.EXE")]
    public void RejectsMalformedBuildsAndPaths(string build, string path) =>
        Assert.Throws<ArgumentException>(() => OriginalGameFiles.Resolve(root, build, path, Hash([1])));

    [Fact]
    public void RejectsHashesNotWrittenTheWayTheSpecWritesThem() =>
        Assert.Throws<ArgumentException>(() =>
            OriginalGameFiles.Resolve(root, Build, "GAME.EXE", Hash([1]).ToUpperInvariant()));

    private string Write(string relative, byte[] bytes)
    {
        var file = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, bytes);
        return Hash(bytes);
    }

    private static string Hash(byte[] bytes) => SpecHash.Xxh3(bytes);
}
