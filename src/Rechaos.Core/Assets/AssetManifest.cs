namespace Rechaos.Core.Assets;

public sealed record AssetManifest(
    int FormatVersion,
    string Product,
    string SourceFingerprintSha256,
    DateTimeOffset ExtractedAtUtc,
    IReadOnlyList<ExtractedAsset> Files,
    string ExtractorVersion = "unknown")
{
    public const int CurrentFormatVersion = 3;
}

public sealed record ExtractedAsset(
    string Path,
    long Bytes,
    string Sha256,
    string SourcePath = "unknown",
    string MediaType = "application/octet-stream",
    AssetConversion? Conversion = null);

public sealed record AssetConversion(
    string Method,
    int? Width = null,
    int? Height = null,
    int? BitsPerPixel = null,
    string? PixelFormat = null);
