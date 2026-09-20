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
                .Append(Escape(DescribeConversion(asset))).Append(" | ")
                .Append(classification.Role).Append(" | ")
                .Append(classification.Owner).Append(" | ")
                .Append(classification.Palette).Append(" | `")
                .Append(asset.Sha256).AppendLine("` |");
        }
        return builder.ToString().ReplaceLineEndings("\n");
    }

    private static (string Role, string Owner, string Palette) Classify(ExtractedAsset asset)
    {
        var resourceName = Path.GetFileNameWithoutExtension(asset.Path).ToUpperInvariant();
        var knownPresentation = KnownPresentation(resourceName);
        var keyedCopy = resourceName is "PX00150" or "PX06004";
        var mixedCopy = resourceName == "PX00129";
        var opaqueBaseScreen = resourceName is "PX00144" or "PX00145" or "PX00146";
        var opaqueBlackCopy = resourceName is "PX00201" or "PX00300" ||
                              resourceName.StartsWith("PX04", StringComparison.Ordinal) ||
                              resourceName.StartsWith("PX07", StringComparison.Ordinal);

        if (asset.Path.StartsWith("images/", StringComparison.Ordinal))
            return (knownPresentation?.Role ?? "PX16 presentation image",
                knownPresentation?.Owner ?? "Unknown", DescribePx16Copy(keyedCopy, mixedCopy, opaqueBaseScreen, opaqueBlackCopy));
        if (asset.Path.StartsWith("images8/", StringComparison.Ordinal))
            return (knownPresentation?.Role ?? "Decoded PX08 presentation image",
                knownPresentation?.Owner ?? "Unknown", DescribePx08Copy(keyedCopy, mixedCopy, opaqueBaseScreen, opaqueBlackCopy, decoded: true));
        if (asset.Path.StartsWith("raw/px08/", StringComparison.Ordinal))
            return (knownPresentation is { } presentation
                    ? presentation.Role + " source"
                    : "PX08 indexed image source",
                knownPresentation?.Owner ?? "Unknown", DescribePx08Copy(keyedCopy, mixedCopy, opaqueBaseScreen, opaqueBlackCopy, decoded: false));
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

    /// <summary>
    /// Presentation identities established by the address-level native research log and UI atlas.
    /// </summary>
    /// <remarks>
    /// Keep this intentionally small and factual. A generic image is still marked unknown until a
    /// resource load/caller path or an independently corroborated atlas mapping identifies it;
    /// naming an attractive sheet from pixels alone would turn the catalog into speculation.
    /// </remarks>
    private static (string Role, string Owner)? KnownPresentation(string resourceName) => resourceName switch
    {
        "PX00100" => ("Publisher/developer credits screen", "Native Help > About; deliberately unrouted"),
        "PX00128" => ("Main city and control-panel canvas", "City/Sector UI"),
        "PX00129" => ("Shared UI composite atlas", "City, panel, font, and status compositors"),
        "PX00130" => ("Title/logo/copyright canvas", "Title screen"),
        "PX00131" => ("Dormant limited/demo promotion", "No caller in supported executable; deliberately unrouted"),
        "PX00132" => ("Next-player handoff panel", "Local hot-seat handoff"),
        "PX00137" => ("Legacy transfer-progress frame", "Unsupported original transport"),
        "PX00138" => ("Legacy transfer spinner", "Unsupported original transport"),
        "PX00139" => ("Legacy synchronization-progress frame", "Unsupported original transport"),
        "PX00140" => ("Local-setup control sheet", "Local setup"),
        "PX00143" => ("Local objective/player setup canvas", "Local setup"),
        "PX00144" => ("Legacy network host lobby", "Unsupported original transport"),
        "PX00145" => ("Legacy compact session editor", "Unsupported original transport"),
        "PX00146" => ("Legacy participant-ready screen", "Unsupported original transport"),
        "PX00150" => ("City site-marker sheet", "Search and city marker renderer"),
        "PX00200" => ("Endgame awards/statistics frame", "Endgame"),
        "PX00201" => ("Endgame award and statistics sprites", "Endgame"),
        "PX00202" => ("Single-human victory splash", "Endgame"),
        "PX00203" => ("Private elimination splash", "Endgame and hot-seat elimination"),
        "PX00300" => ("Police combat sprite sheet", "Combat compositor"),
        "PX02000" => ("Site portrait strip", "Site, sector, Search, and event panels"),
        "PX03000" => ("Gang portrait grid", "Gang, sector, hire, and combat panels"),
        _ => null
    };

    private static string DescribePx16Copy(bool keyedCopy, bool mixedCopy, bool opaqueBaseScreen, bool opaqueBlackCopy)
    {
        if (keyedCopy) return "RGB555; native exact maximum-white key";
        if (mixedCopy) return "RGB555; native mixed opaque, pattern-mask, and exact-white copies";
        if (opaqueBaseScreen) return "RGB555; opaque base screen";
        if (opaqueBlackCopy) return "RGB555; native opaque copy (black retained)";
        return "RGB555; color key unresolved";
    }

    private static string DescribePx08Copy(
        bool keyedCopy, bool mixedCopy, bool opaqueBaseScreen, bool opaqueBlackCopy, bool decoded)
    {
        var palette = decoded ? "Embedded 256-entry BGRA palette" : "Embedded indexed palette";
        if (keyedCopy) return $"{palette}; native exact white key";
        if (mixedCopy) return $"{palette}; native mixed opaque, pattern-mask, and exact-white copies";
        if (opaqueBaseScreen) return $"{palette}; opaque base screen";
        if (opaqueBlackCopy) return $"{palette}; native opaque copy (black retained)";
        return decoded
            ? "Embedded 256-entry BGRA palette; transparency unresolved"
            : "Palette and transparency unresolved";
    }

    private static string DescribeConversion(ExtractedAsset asset)
    {
        if (asset.Path.Equals("video/MVINTRO.smk", StringComparison.OrdinalIgnoreCase))
            return "copy; validated Smacker v2; 480x256; 1,150 frames; 100 ms/frame; packed PCM 22050 Hz 8-bit stereo";
        if (asset.Path.Equals("video/MVLOGOS.smk", StringComparison.OrdinalIgnoreCase))
            return "copy; validated Smacker v2; 480x256; 200 frames; 100 ms/frame; packed PCM 22050 Hz 8-bit mono";

        var conversion = asset.Conversion;
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
