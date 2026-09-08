using System.Security.Cryptography;
using System.Text.Json;
using Rechaos.Core.Assets;

namespace Rechaos.Extractor;

public sealed record AssetPackVerification(
    bool IsValid,
    AssetManifest? Manifest,
    IReadOnlyList<string> Errors,
    int VerifiedFiles);

public static class AssetPackVerifier
{
    public static async Task<AssetPackVerification> VerifyAsync(
        string assetRoot,
        int expectedFormatVersion,
        bool verifyHashes = true,
        int expectedFileCount = 0)
    {
        assetRoot = Path.GetFullPath(assetRoot);
        var errors = new List<string>();
        var manifestPath = Path.Combine(assetRoot, "manifest.json");
        if (!File.Exists(manifestPath))
            return Invalid($"Manifest not found: {manifestPath}");

        AssetManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<AssetManifest>(stream);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return Invalid($"Manifest cannot be read: {exception.Message}");
        }

        if (manifest is null) return Invalid("Manifest is empty.");
        if (manifest.FormatVersion != expectedFormatVersion)
            errors.Add($"Format version {manifest.FormatVersion} is incompatible; expected {expectedFormatVersion}.");
        if (!IsSha256(manifest.SourceFingerprintSha256))
            errors.Add("Source fingerprint is missing or malformed.");
        if (manifest.Files is null) return Invalid("Manifest asset list is missing.");
        if (manifest.Files.Count == 0) errors.Add("Manifest contains no assets.");
        if (expectedFileCount > 0 && manifest.Files.Count != expectedFileCount)
            errors.Add($"Manifest contains {manifest.Files.Count} assets; expected {expectedFileCount}.");

        var rootWithSeparator = assetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var verified = 0;
        foreach (var asset in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(asset.Path))
            {
                errors.Add("Asset path is missing.");
                continue;
            }
            if (!paths.Add(asset.Path))
            {
                errors.Add($"Duplicate manifest path: {asset.Path}");
                continue;
            }
            if (!IsSha256(asset.Sha256))
            {
                errors.Add($"Asset hash is missing or malformed: {asset.Path}");
                continue;
            }

            string path;
            try { path = Path.GetFullPath(Path.Combine(assetRoot, asset.Path)); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add($"Invalid manifest path '{asset.Path}': {exception.Message}");
                continue;
            }
            if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Manifest path escapes the asset directory: {asset.Path}");
                continue;
            }
            if (!File.Exists(path))
            {
                errors.Add($"Asset is missing: {asset.Path}");
                continue;
            }
            var length = new FileInfo(path).Length;
            if (length != asset.Bytes)
            {
                errors.Add($"Asset size mismatch: {asset.Path} (expected {asset.Bytes}, found {length})");
                continue;
            }
            if (verifyHashes)
            {
                await using var stream = File.OpenRead(path);
                var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
                if (!string.Equals(hash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Asset hash mismatch: {asset.Path}");
                    continue;
                }
            }
            verified++;
        }
        return new AssetPackVerification(errors.Count == 0, manifest, errors, verified);

        AssetPackVerification Invalid(string error) => new(false, null, [error], 0);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}
