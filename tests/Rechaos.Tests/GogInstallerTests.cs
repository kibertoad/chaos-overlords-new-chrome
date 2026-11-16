using Rechaos.Extractor;
using Rechaos.Extractor.Installer;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Reading the game's folders out of GOG's installer (SRC-INSTALLER-GOG). The tests that need the
/// installer read it from <c>GAME_DIR/captures/</c>, named by the xxh3 SRC-INSTALLER-GOG records,
/// and are skipped without it.
/// </summary>
public sealed class GogInstallerTests : IDisposable
{
    private const string InstallerXxh3 = "f03388775137c86a96cd85a36f4dae9d";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"rechaos-gog-installer-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Theory]
    [InlineData(@"{app}\DATA\PX16\PX00100", "DATA/PX16/PX00100")]
    [InlineData(@"{app}\data\Gangs", "data/Gangs")]
    [InlineData(@"{app}\MUSIC\track01.ogg", "MUSIC/track01.ogg")]
    [InlineData(@"{app}\HELP\Chaos.hlp", "HELP/Chaos.hlp")]
    public void TakesTheFilesOfTheGameFolders(string destination, string expected)
    {
        Assert.Equal(Path.Combine(expected.Split('/')), GogInstaller.GameFolderPath(destination));
    }

    [Theory]
    [InlineData(@"{app}\Chaos Overlords.exe")]
    [InlineData(@"{app}\DATA")]
    [InlineData(@"{app}\SAVES\game1.sav")]
    [InlineData(@"{tmp}\DATA\Gangs")]
    [InlineData("")]
    public void LeavesOtherFilesOut(string destination)
    {
        Assert.Null(GogInstaller.GameFolderPath(destination));
    }

    [Theory]
    [InlineData(@"{app}\DATA\..\..\outside")]
    [InlineData(@"{app}\DATA\\Gangs")]
    public void RefusesAPathThatLeavesItsFolder(string destination)
    {
        Assert.Throws<InvalidDataException>(() => GogInstaller.GameFolderPath(destination));
    }

    [Fact]
    public async Task RefusesAFileThatIsNotTheInstaller()
    {
        Directory.CreateDirectory(_root);
        var other = Path.Combine(_root, "setup_chaos_overlords_2.0.0.1.exe");
        await File.WriteAllBytesAsync(other, new byte[1024], TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GogInstaller.UnpackGameFoldersAsync(other, Path.Combine(_root, "out")));

        Assert.Contains(GogInstaller.FileName, error.Message, StringComparison.Ordinal);
        Assert.Contains("innoextract", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_root, "out")));
    }

    [Fact]
    public async Task UnpacksTheFoldersOfTheSupportedInstallation()
    {
        var installer = OriginalGameFiles.RequireCapture(InstallerXxh3);

        var count = await GogInstaller.UnpackGameFoldersAsync(installer, _root);

        Assert.Equal(474, count);
        Assert.Equal(GogInstaller.GameFolders, Directory.EnumerateDirectories(_root).Select(path => Path.GetFileName(path)).Order());
        // The check an installed folder passes: the tables' hashes, the record structure and the
        // fingerprint of every file in DATA, MUSIC and HELP.
        var pack = await ExtractorProgram.VerifySourceAsync(_root);
        Assert.Equal(_root, pack.Root);
    }

    [Fact]
    public async Task TheExtractorTakesTheInstallerAsItsSourceAndLeavesNoUnpackedCopy()
    {
        var installer = OriginalGameFiles.RequireCapture(InstallerXxh3);
        var before = UnpackedCopies();

        var exitCode = await ExtractorProgram.RunAsync(["--verify-source", "--source", installer]);

        Assert.Equal(0, exitCode);
        Assert.Equal(before, UnpackedCopies());
    }

    private static string[] UnpackedCopies() =>
        [.. Directory.EnumerateDirectories(Path.GetTempPath(), "rechaos-installer-*").Order()];
}
