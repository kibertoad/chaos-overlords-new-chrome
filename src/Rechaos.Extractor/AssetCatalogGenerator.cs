using System.Text;
using Rechaos.Core.Assets;

namespace Rechaos.Extractor;

public static class AssetCatalogGenerator
{
    public static async Task WriteAsync(AssetManifest manifest, string output)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, Generate(manifest), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static string Generate(AssetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var builder = new StringBuilder();
        builder.AppendLine("# Original asset catalog");
        builder.AppendLine();
        builder.AppendLine("Status: generated inventory; semantic ownership remains incomplete");
        builder.AppendLine($"Manifest format: {manifest.FormatVersion}");
        builder.AppendLine($"Extractor version: {Escape(manifest.ExtractorVersion)}");
        builder.AppendLine($"Source fingerprint: `{manifest.SourceFingerprintSha256}`");
        builder.AppendLine($"Asset count: {manifest.Files.Count}");
        builder.AppendLine();
        builder.AppendLine("This file is generated from a fully verified local manifest. Resource names and hashes are factual inventory metadata; no proprietary media bytes are stored here. `Unknown` fields must be resolved through atlas/media research rather than guessed.");
        builder.AppendLine();
        builder.AppendLine("| Output | Original source | Media type | Conversion / geometry | Semantic role | Screen/action owner | Palette / transparency | SHA-256 |");
        builder.AppendLine("|---|---|---|---|---|---|---|---|");

        foreach (var asset in manifest.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            var classification = Classify(asset);
            builder.Append("| `").Append(Escape(asset.Path)).Append("` | `")
                .Append(Escape(asset.SourcePath)).Append("` | ")
                .Append(Escape(asset.MediaType)).Append(" | ")
                .Append(Escape(DescribeConversion(asset.Conversion))).Append(" | ")
                .Append(classification.Role).Append(" | ")
                .Append(classification.Owner).Append(" | ")
                .Append(classification.Palette).Append(" | `")
                .Append(asset.Sha256).AppendLine("` |");
        }
        return builder.ToString();
    }

    private static (string Role, string Owner, string Palette) Classify(ExtractedAsset asset)
    {
        if (asset.Path.StartsWith("images/", StringComparison.Ordinal))
            return ("PX16 presentation image", "Unknown", "RGB555; color key unresolved");
        if (asset.Path.StartsWith("images8/", StringComparison.Ordinal))
            return ("Decoded PX08 presentation image", "Unknown", "Embedded 256-entry BGRA palette; transparency unresolved");
        if (asset.Path.StartsWith("raw/px08/", StringComparison.Ordinal))
            return ("PX08 indexed image source", "Unknown", "Palette and transparency unresolved");
        if (asset.Path.StartsWith("audio/", StringComparison.Ordinal))
            return ("Sound effect", "Unknown action/UI trigger", "N/A");
        if (asset.Path.StartsWith("music/", StringComparison.Ordinal))
            return ("Music track", "Unknown sequencing/loop owner", "N/A");
        if (asset.Path.StartsWith("video/", StringComparison.Ordinal))
            return ("Smacker video", asset.Path.Contains("INTRO", StringComparison.Ordinal) ? "Intro" : "Logo flow", "N/A");
        if (asset.Path.Equals("help/contents.json", StringComparison.Ordinal))
            return ("Decoded WinHelp topics and contents", "Modern in-game help viewer", "N/A");
        if (asset.Path.StartsWith("help/", StringComparison.Ordinal))
            return ("Original WinHelp resource", "Local extraction input; not used at runtime", "Format-dependent");
        return ("Opaque original data", "Unknown", "Unknown");
    }

    private static string DescribeConversion(AssetConversion? conversion)
    {
        if (conversion is null) return "Unknown";
        var geometry = conversion.Width is not null && conversion.Height is not null
            ? $"; {conversion.Width}x{conversion.Height}x{conversion.BitsPerPixel} ({conversion.PixelFormat})"
            : string.Empty;
        return conversion.Method + geometry;
    }

    private static string Escape(string? value) =>
        (value ?? "Unknown").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}

public static class MediaTypes
{
    public static string ForPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".bmp" => "image/bmp",
        ".gif" => "image/gif",
        ".htm" or ".html" => "text/html",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".wav" => "audio/wav",
        _ => "application/octet-stream"
    };
}
