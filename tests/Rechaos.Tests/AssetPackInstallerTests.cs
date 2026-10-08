using System.Security.Cryptography;
using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

public sealed class AssetPackInstallerTests : IDisposable
{
    private readonly string _parent = Path.Combine(Path.GetTempPath(), "rechaos-installer-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task VerifiedStageReplacesExistingPack()
    {
        var output = Path.Combine(_parent, "assets");
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "old.txt"), "old", TestContext.Current.CancellationToken);

        var result = await AssetPackInstaller.InstallAsync(
            output, ExtractorProgram.FormatVersion, expectedFileCount: 1, WriteValidPackAsync);

        Assert.Single(result.Files);
        Assert.False(File.Exists(Path.Combine(output, "old.txt")));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(
            Path.Combine(output, "new.bin"), TestContext.Current.CancellationToken));
        Assert.Empty(TemporaryDirectories());
    }

    [Fact]
    public async Task InvalidStageLeavesExistingPackUntouched()
    {
        var output = Path.Combine(_parent, "assets");
        Directory.CreateDirectory(output);
        var oldPath = Path.Combine(output, "old.txt");
        await File.WriteAllTextAsync(oldPath, "old", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => AssetPackInstaller.InstallAsync(
            output, ExtractorProgram.FormatVersion, expectedFileCount: 2, WriteValidPackAsync));

        Assert.Equal("old", await File.ReadAllTextAsync(oldPath, TestContext.Current.CancellationToken));
        Assert.Empty(TemporaryDirectories());
    }

    [Fact]
    public async Task StagingFailureLeavesExistingPackUntouched()
    {
        var output = Path.Combine(_parent, "assets");
        Directory.CreateDirectory(output);
        var oldPath = Path.Combine(output, "old.txt");
        await File.WriteAllTextAsync(oldPath, "old", TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<OutputNotWritableException>(() => AssetPackInstaller.InstallAsync(
            output, ExtractorProgram.FormatVersion, expectedFileCount: 1,
            _ => throw new IOException("synthetic extraction failure")));

        Assert.Contains("synthetic extraction failure", exception.Message, StringComparison.Ordinal);

        Assert.Equal("old", await File.ReadAllTextAsync(oldPath, TestContext.Current.CancellationToken));
        Assert.Empty(TemporaryDirectories());
    }

    [Fact]
    public async Task UnmanifestedStagedFileLeavesExistingPackUntouched()
    {
        var output = Path.Combine(_parent, "assets");
        Directory.CreateDirectory(output);
        var oldPath = Path.Combine(output, "old.txt");
        await File.WriteAllTextAsync(oldPath, "old", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => AssetPackInstaller.InstallAsync(
            output, ExtractorProgram.FormatVersion, expectedFileCount: 1, async staging =>
            {
                var manifest = await WriteValidPackAsync(staging);
                await File.WriteAllTextAsync(Path.Combine(staging, "stale.bin"), "stale",
                    TestContext.Current.CancellationToken);
                return manifest;
            }));

        Assert.Equal("old", await File.ReadAllTextAsync(oldPath, TestContext.Current.CancellationToken));
        Assert.Empty(TemporaryDirectories());
    }

    [Fact]
    public void AnOutputThatCannotBeCreatedIsReportedBeforeAnyExtraction()
    {
        Directory.CreateDirectory(_parent);
        var blocker = Path.Combine(_parent, "blocker");
        File.WriteAllText(blocker, "a file where the output's parent folder would go");

        var exception = Assert.Throws<OutputNotWritableException>(
            () => AssetPackInstaller.EnsureWritable(Path.Combine(blocker, "assets")));

        Assert.Contains(blocker, exception.Message, StringComparison.Ordinal);
        Assert.Equal(["blocker"], Directory.EnumerateFileSystemEntries(_parent).Select(Path.GetFileName));
    }

    [Fact]
    public void AWritableOutputLeavesNoProbeBehind()
    {
        var output = Path.Combine(_parent, "assets");

        AssetPackInstaller.EnsureWritable(output);

        Assert.Empty(Directory.EnumerateFileSystemEntries(_parent));
    }

    private static async Task<AssetManifest> WriteValidPackAsync(string staging)
    {
        var bytes = new byte[] { 1, 2, 3 };
        var assetPath = Path.Combine(staging, "new.bin");
        await File.WriteAllBytesAsync(assetPath, bytes, TestContext.Current.CancellationToken);
        var manifest = new AssetManifest(
            ExtractorProgram.FormatVersion,
            "test",
            new string('a', 64),
            DateTimeOffset.UnixEpoch,
            [new ExtractedAsset("new.bin", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))]);
        await File.WriteAllTextAsync(
            Path.Combine(staging, "manifest.json"),
            JsonSerializer.Serialize(manifest),
            TestContext.Current.CancellationToken);
        return manifest;
    }

    private IEnumerable<string> TemporaryDirectories() =>
        Directory.Exists(_parent)
            ? Directory.EnumerateDirectories(_parent).Where(path => Path.GetFileName(path).StartsWith(".assets.", StringComparison.Ordinal))
            : [];

    public void Dispose()
    {
        if (Directory.Exists(_parent)) Directory.Delete(_parent, recursive: true);
    }
}
