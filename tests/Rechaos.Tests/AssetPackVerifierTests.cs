using System.Security.Cryptography;
using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Extractor;
using Xunit;

namespace Rechaos.Tests;

public sealed class AssetPackVerifierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rechaos-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CompletePackPassesAndCorruptionFails()
    {
        var assetPath = Path.Combine(_root, "images", "test.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllBytesAsync(assetPath, [1, 2, 3, 4], TestContext.Current.CancellationToken);
        var hash = Convert.ToHexStringLower(SHA256.HashData([1, 2, 3, 4]));
        await WriteManifestAsync([new ExtractedAsset("images/test.bin", 4, hash)]);

        var valid = await AssetPackVerifier.VerifyAsync(_root, ExtractorProgram.FormatVersion);
        Assert.True(valid.IsValid, string.Join(Environment.NewLine, valid.Errors));
        Assert.Equal(1, valid.VerifiedFiles);

        await File.WriteAllBytesAsync(assetPath, [1, 2, 3, 5], TestContext.Current.CancellationToken);
        var corrupt = await AssetPackVerifier.VerifyAsync(_root, ExtractorProgram.FormatVersion);
        Assert.False(corrupt.IsValid);
        Assert.Contains(corrupt.Errors, error => error.Contains("hash mismatch", StringComparison.OrdinalIgnoreCase));
        var diagnostic = Assert.Single(corrupt.Diagnostics,
            value => value.Code == "asset_hash_mismatch");
        Assert.Equal("images/test.bin", diagnostic.Path);
        Assert.Equal(hash, diagnostic.Expected);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData([1, 2, 3, 5])), diagnostic.Actual);
    }

    [Fact]
    public async Task ManifestCannotEscapeAssetDirectory()
    {
        await WriteManifestAsync([new ExtractedAsset("../outside.bin", 0, new string('0', 64))]);
        var result = await AssetPackVerifier.VerifyAsync(_root, ExtractorProgram.FormatVersion, verifyHashes: false);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("escapes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExpectedCountRejectsSelfConsistentButIncompleteManifest()
    {
        var assetPath = Path.Combine(_root, "one.bin");
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(assetPath, [42], TestContext.Current.CancellationToken);
        await WriteManifestAsync([new ExtractedAsset("one.bin", 1,
            Convert.ToHexStringLower(SHA256.HashData([42]))) ]);

        var result = await AssetPackVerifier.VerifyAsync(_root, ExtractorProgram.FormatVersion,
            verifyHashes: true, expectedFileCount: 2);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("expected 2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MalformedHashFailsEvenInQuickMode()
    {
        var assetPath = Path.Combine(_root, "one.bin");
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(assetPath, [42], TestContext.Current.CancellationToken);
        await WriteManifestAsync([new ExtractedAsset("one.bin", 1, "not-a-hash")]);

        var result = await AssetPackVerifier.VerifyAsync(_root, ExtractorProgram.FormatVersion, verifyHashes: false);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("malformed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnexpectedFilesInvalidatePackEvenInQuickMode()
    {
        var assetPath = Path.Combine(_root, "images", "test.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        await File.WriteAllBytesAsync(assetPath, [1, 2, 3], TestContext.Current.CancellationToken);
        await WriteManifestAsync([new ExtractedAsset("images/test.bin", 3,
            Convert.ToHexStringLower(SHA256.HashData([1, 2, 3]))) ]);
        var stalePath = Path.Combine(_root, "obsolete", "old.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(stalePath)!);
        await File.WriteAllBytesAsync(stalePath, [9], TestContext.Current.CancellationToken);

        var result = await AssetPackVerifier.VerifyAsync(
            _root, ExtractorProgram.FormatVersion, verifyHashes: false);

        Assert.False(result.IsValid);
        Assert.Equal(1, result.VerifiedFiles);
        Assert.Contains(result.Errors, error => error.Contains(
            Path.Combine("obsolete", "old.bin"), StringComparison.OrdinalIgnoreCase));
        var diagnostic = Assert.Single(result.Diagnostics,
            value => value.Code == "unexpected_asset");
        Assert.Equal(Path.Combine("obsolete", "old.bin"), diagnostic.Path);
    }

    [Fact]
    public async Task JsonReportHasStableSchemaModeCountsAndDiagnosticCodes()
    {
        var result = await AssetPackVerifier.VerifyAsync(
            _root, ExtractorProgram.FormatVersion, verifyHashes: false,
            ExtractorProgram.ExpectedExtractedAssetCount);

        using var document = JsonDocument.Parse(
            ExtractorProgram.SerializeVerificationReport(result, _root, quick: true));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(Path.GetFullPath(_root), root.GetProperty("assetRoot").GetString());
        Assert.Equal("quick", root.GetProperty("verificationMode").GetString());
        Assert.False(root.GetProperty("isValid").GetBoolean());
        Assert.Equal(ExtractorProgram.FormatVersion,
            root.GetProperty("expectedFormatVersion").GetInt32());
        Assert.Equal(ExtractorProgram.ExpectedExtractedAssetCount,
            root.GetProperty("expectedFiles").GetInt32());
        Assert.Equal(0, root.GetProperty("verifiedFiles").GetInt32());
        var diagnostic = Assert.Single(root.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("manifest_missing", diagnostic.GetProperty("code").GetString());
        Assert.Equal("manifest.json", diagnostic.GetProperty("path").GetString());
    }

    [Fact]
    public async Task NullManifestFieldsAreReportedInsteadOfThrowing()
    {
        Directory.CreateDirectory(_root);
        const string json = """
            {
              "FormatVersion": 1,
              "Product": "test",
              "SourceFingerprintSha256": null,
              "ExtractedAtUtc": "1970-01-01T00:00:00+00:00",
              "Files": [{ "Path": null, "Bytes": 0, "Sha256": null }]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_root, "manifest.json"), json,
            TestContext.Current.CancellationToken);

        var result = await AssetPackVerifier.VerifyAsync(_root, ExtractorProgram.FormatVersion, verifyHashes: false);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("fingerprint", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("path is missing", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteManifestAsync(IReadOnlyList<ExtractedAsset> files)
    {
        Directory.CreateDirectory(_root);
        var manifest = new AssetManifest(ExtractorProgram.FormatVersion, "test", new string('a', 64),
            DateTimeOffset.UnixEpoch, files);
        await File.WriteAllTextAsync(Path.Combine(_root, "manifest.json"), JsonSerializer.Serialize(manifest),
            TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
