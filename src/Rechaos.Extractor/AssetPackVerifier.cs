using System.Security.Cryptography;
using System.Text.Json;
using Rechaos.Core.Assets;

namespace Rechaos.Extractor;

public sealed record AssetPackVerification(
    bool IsValid,
    AssetManifest? Manifest,
    IReadOnlyList<AssetPackDiagnostic> Diagnostics,
    int VerifiedFiles)
{
    public IReadOnlyList<string> Errors => Diagnostics.Select(diagnostic => diagnostic.Message).ToArray();
}

public sealed record AssetPackDiagnostic(
    string Code,
    string Message,
    string? Path = null,
    string? Expected = null,
    string? Actual = null);

public static class AssetPackVerifier
{
    public static async Task<AssetPackVerification> VerifyAsync(
        string assetRoot,
        int expectedFormatVersion,
        bool verifyHashes = true,
        int expectedFileCount = 0)
    {
        assetRoot = Path.GetFullPath(assetRoot);
        var diagnostics = new List<AssetPackDiagnostic>();
        var manifestPath = Path.Combine(assetRoot, "manifest.json");
        if (!File.Exists(manifestPath))
            return Invalid("manifest_missing", $"Manifest not found: {manifestPath}", "manifest.json");

        AssetManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            manifest = await JsonSerializer.DeserializeAsync<AssetManifest>(stream);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return Invalid("manifest_unreadable", $"Manifest cannot be read: {exception.Message}", "manifest.json");
        }

        if (manifest is null) return Invalid("manifest_empty", "Manifest is empty.", "manifest.json");
        if (manifest.FormatVersion != expectedFormatVersion)
            Add("format_version_mismatch",
                $"Format version {manifest.FormatVersion} is incompatible; expected {expectedFormatVersion}.",
                "manifest.json", expectedFormatVersion.ToString(), manifest.FormatVersion.ToString());
        if (!IsSha256(manifest.SourceFingerprintSha256))
            Add("source_fingerprint_malformed", "Source fingerprint is missing or malformed.", "manifest.json");
        if (string.IsNullOrWhiteSpace(manifest.ExtractorVersion))
            Add("extractor_version_missing", "Extractor version is missing.", "manifest.json");
        if (manifest.Files is null)
            return Invalid("asset_list_missing", "Manifest asset list is missing.", "manifest.json");
        if (manifest.Files.Count == 0)
            Add("asset_list_empty", "Manifest contains no assets.", "manifest.json");
        if (expectedFileCount > 0 && manifest.Files.Count != expectedFileCount)
            Add("asset_count_mismatch",
                $"Manifest contains {manifest.Files.Count} assets; expected {expectedFileCount}.",
                "manifest.json", expectedFileCount.ToString(), manifest.Files.Count.ToString());

        var rootWithSeparator = assetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectedPaths = new HashSet<string>(PathComparer);
        var verified = 0;
        foreach (var asset in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(asset.Path))
            {
                Add("asset_path_missing", "Asset path is missing.");
                continue;
            }
            if (!paths.Add(asset.Path))
            {
                Add("duplicate_asset_path", $"Duplicate manifest path: {asset.Path}", asset.Path);
                continue;
            }
            if (!IsSha256(asset.Sha256))
            {
                Add("asset_hash_malformed", $"Asset hash is missing or malformed: {asset.Path}", asset.Path);
                continue;
            }
            if (string.IsNullOrWhiteSpace(asset.SourcePath))
                Add("source_path_missing", $"Original source path is missing: {asset.Path}", asset.Path);
            if (string.IsNullOrWhiteSpace(asset.MediaType))
                Add("media_type_missing", $"Media type is missing: {asset.Path}", asset.Path);
            if (asset.Conversion is not null && string.IsNullOrWhiteSpace(asset.Conversion.Method))
                Add("conversion_method_missing", $"Conversion method is missing: {asset.Path}", asset.Path);

            string path;
            try { path = Path.GetFullPath(Path.Combine(assetRoot, asset.Path)); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Add("asset_path_invalid", $"Invalid manifest path '{asset.Path}': {exception.Message}", asset.Path);
                continue;
            }
            if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                Add("asset_path_escape", $"Manifest path escapes the asset directory: {asset.Path}", asset.Path);
                continue;
            }
            expectedPaths.Add(path);
            if (!File.Exists(path))
            {
                Add("asset_missing", $"Asset is missing: {asset.Path}", asset.Path);
                continue;
            }
            var length = new FileInfo(path).Length;
            if (length != asset.Bytes)
            {
                Add("asset_size_mismatch",
                    $"Asset size mismatch: {asset.Path} (expected {asset.Bytes}, found {length})",
                    asset.Path, asset.Bytes.ToString(), length.ToString());
                continue;
            }
            if (verifyHashes)
            {
                await using var stream = File.OpenRead(path);
                var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
                if (!string.Equals(hash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Add("asset_hash_mismatch", $"Asset hash mismatch: {asset.Path}", asset.Path,
                        asset.Sha256, hash);
                    continue;
                }
            }
            verified++;
        }
        try
        {
            foreach (var installedPath in Directory.EnumerateFiles(
                         assetRoot, "*", SearchOption.AllDirectories))
            {
                var fullPath = Path.GetFullPath(installedPath);
                if (PathComparer.Equals(fullPath, manifestPath)) continue;
                if (!expectedPaths.Contains(fullPath))
                {
                    var relative = Path.GetRelativePath(assetRoot, fullPath);
                    Add("unexpected_asset", $"Unexpected asset file: {relative}", relative);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Add("asset_inventory_unreadable",
                $"Asset directory cannot be completely inventoried: {exception.Message}");
        }
        return new AssetPackVerification(diagnostics.Count == 0, manifest, diagnostics, verified);

        void Add(
            string code,
            string message,
            string? path = null,
            string? expected = null,
            string? actual = null) =>
            diagnostics.Add(new AssetPackDiagnostic(code, message, path, expected, actual));

        AssetPackVerification Invalid(string code, string message, string? path = null) =>
            new(false, null, [new AssetPackDiagnostic(code, message, path)], 0);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
