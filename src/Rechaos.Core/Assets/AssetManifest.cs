namespace Rechaos.Core.Assets;

public sealed record AssetManifest(
    int FormatVersion,
    string Product,
    string SourceFingerprintSha256,
    DateTimeOffset ExtractedAtUtc,
    IReadOnlyList<ExtractedAsset> Files);

public sealed record ExtractedAsset(string Path, long Bytes, string Sha256);
