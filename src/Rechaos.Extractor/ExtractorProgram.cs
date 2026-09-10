using System.Security.Cryptography;
using System.Text.Json;
using Rechaos.Core.Assets;

namespace Rechaos.Extractor;

public static class ExtractorProgram
{
    public const int FormatVersion = AssetManifest.CurrentFormatVersion;
    public const int ExpectedExtractedAssetCount = 686;
    private const string KnownSourceFingerprintSha256 = "ad958a934a691318f31a27a87252f420dd89a0ad03759457d8feaf49914d29e3";
    private static readonly IReadOnlyDictionary<string, string> KnownTableSha256 =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SITES"] = GameplayDataProvenance.SitesSha256,
            ["GANGS"] = GameplayDataProvenance.GangsSha256,
            ["ITEMS"] = GameplayDataProvenance.ItemsSha256
        };

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            if (options.Mode == ExtractorMode.VerifyOutput)
            {
                var verification = await AssetPackVerifier.VerifyAsync(options.Output, FormatVersion,
                    !options.Quick, ExpectedExtractedAssetCount);
                PrintVerification(verification, options.Output);
                return verification.IsValid ? 0 : 1;
            }
            if (options.Mode == ExtractorMode.GenerateCatalog)
            {
                var verification = await AssetPackVerifier.VerifyAsync(options.Output, FormatVersion,
                    verifyHashes: true, ExpectedExtractedAssetCount);
                if (!verification.IsValid || verification.Manifest is null)
                    throw new InvalidDataException("Cannot generate catalog from an invalid pack: " +
                        string.Join("; ", verification.Errors));
                var catalogOutput = options.CatalogOutput
                    ?? Path.Combine("docs", "ASSET-CATALOG.md");
                await AssetCatalogGenerator.WriteAsync(verification.Manifest, catalogOutput);
                Console.WriteLine($"Generated catalog for {verification.Manifest.Files.Count} assets at {Path.GetFullPath(catalogOutput)}");
                return 0;
            }
            if (options.Mode == ExtractorMode.AnalyzePxColor)
            {
                var verification = await AssetPackVerifier.VerifyAsync(options.Output, FormatVersion,
                    verifyHashes: true, ExpectedExtractedAssetCount);
                if (!verification.IsValid)
                    throw new InvalidDataException("Cannot analyze an invalid pack: " + string.Join("; ", verification.Errors));
                var comparison = AnalyzePxColor(options.Output);
                Console.WriteLine($"Compared {comparison.PixelCount} paired pixels across 214 resources.");
                Console.WriteLine($"RGB555 absolute error: {comparison.Rgb555AbsoluteError}; exact pixels: {comparison.Rgb555ExactPixels}");
                Console.WriteLine($"RGB565 absolute error: {comparison.Rgb565AbsoluteError}; exact pixels: {comparison.Rgb565ExactPixels}");
                Console.WriteLine($"High bit set: {comparison.HighBitSetPixels} pixels.");
                return comparison.Rgb555AbsoluteError < comparison.Rgb565AbsoluteError ? 0 : 1;
            }

            var source = options.Source
                ?? throw new ArgumentException("--source is required; the port does not distribute original assets.");
            Console.WriteLine($"Checking the original Chaos Overlords installation at {Path.GetFullPath(source)}...");
            var sourcePack = await VerifySourceAsync(source);
            if (options.Mode == ExtractorMode.VerifySource)
            {
                Console.WriteLine($"Supported original asset pack: {sourcePack.Fingerprint}");
                return 0;
            }

            var existing = await AssetPackVerifier.VerifyAsync(options.Output, FormatVersion,
                expectedFileCount: ExpectedExtractedAssetCount);
            if (!options.Force && existing.IsValid && string.Equals(existing.Manifest?.SourceFingerprintSha256,
                    sourcePack.Fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Asset pack is already complete at {Path.GetFullPath(options.Output)}; extraction skipped.");
                return 0;
            }

            Console.WriteLine("Original installation verified. Importing assets...");
            var manifest = await InstallVerifiedAsync(sourcePack, options.Output);
            Console.WriteLine($"Extracted and verified {manifest.Files.Count} assets to {Path.GetFullPath(options.Output)}");
            return 0;
        }
        catch (ArgumentException exception) { Console.Error.WriteLine(exception.Message); return 2; }
        catch (Exception exception) { Console.Error.WriteLine($"Extraction failed: {exception.Message}"); return 1; }
    }

    public static async Task<AssetManifest> ExtractAsync(string source, string output)
    {
        var sourcePack = await VerifySourceAsync(source);
        return await InstallVerifiedAsync(sourcePack, output);
    }

    private static Task<AssetManifest> InstallVerifiedAsync(OriginalAssetPack source, string output) =>
        AssetPackInstaller.InstallAsync(output, FormatVersion, ExpectedExtractedAssetCount,
            staging => ExtractToAsync(source, staging));

    public static async Task<OriginalAssetPack> VerifySourceAsync(string source)
    {
        source = Path.GetFullPath(source);
        var sourceContainsDataDirectory = Directory.Exists(Path.Combine(source, "DATA"));
        var dataDirectory = sourceContainsDataDirectory ? Path.Combine(source, "DATA") : source;
        var installRoot = sourceContainsDataDirectory ? source : Directory.GetParent(source)?.FullName ?? source;
        var musicDirectory = Path.Combine(installRoot, "MUSIC");
        var helpDirectory = Path.Combine(installRoot, "HELP");
        if (!Directory.Exists(Path.Combine(dataDirectory, "PX16")))
            throw new InvalidDataException("The selected folder is not a Chaos Overlords asset pack (DATA/PX16 is missing).");
        foreach (var expected in KnownTableSha256)
        {
            var table = OriginalDataReader.FindCaseInsensitive(Path.Combine(dataDirectory, expected.Key));
            var actual = await HashAsync(table);
            if (!string.Equals(actual, expected.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsupported or modified {expected.Key} table (SHA-256 {actual}).");
        }

        _ = OriginalDataReader.Read(dataDirectory); // Validate exact record structure before extraction.
        var fingerprint = await SourceFingerprint.ComputeAsync(dataDirectory, musicDirectory, helpDirectory);
        if (!string.Equals(fingerprint, KnownSourceFingerprintSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported or incomplete original asset pack (SHA-256 {fingerprint}).");
        return new OriginalAssetPack(source, dataDirectory, musicDirectory, helpDirectory, fingerprint);
    }

    private static async Task<AssetManifest> ExtractToAsync(OriginalAssetPack source, string output)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var files = new List<ExtractedAsset>();
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        Console.WriteLine("Importing artwork...");
        var px16 = Path.Combine(source.DataDirectory, "PX16");
        foreach (var sourceImage in Directory.EnumerateFiles(px16).OrderBy(Path.GetFileName))
        {
            var name = Path.GetFileName(sourceImage).ToUpperInvariant();
            if (!PxDimensions.TryGet(name, new FileInfo(sourceImage).Length, out var size)) continue;
            var destination = Path.Combine(output, "images", name + ".bmp");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var bytes = await File.ReadAllBytesAsync(sourceImage);
            BmpRepair.Repair(bytes, size.Width, size.Height, 16);
            await File.WriteAllBytesAsync(destination, bytes);
            files.Add(await DescribeAsync(output, destination, $"DATA/PX16/{name}", "image/bmp",
                new AssetConversion("bmp-header-repair", size.Width, size.Height, 16, "RGB555")));
        }

        Console.WriteLine("Importing sound and music...");
        foreach (var sound in Directory.EnumerateFiles(source.DataDirectory, "SND*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(sound).ToUpperInvariant();
            files.Add(await CopyAsync(output, sound, Path.Combine(output, "audio", name + ".wav"),
                $"DATA/{name}", "audio/wav"));
        }
        if (Directory.Exists(source.MusicDirectory))
            foreach (var music in Directory.EnumerateFiles(source.MusicDirectory, "*.ogg"))
            {
                var name = Path.GetFileName(music).ToLowerInvariant();
                files.Add(await CopyAsync(output, music, Path.Combine(output, "music", name),
                    $"MUSIC/{name}", "audio/ogg"));
            }
        Console.WriteLine("Importing video and game data...");
        foreach (var videoName in new[] { "MVINTRO", "MVLOGOS" })
        {
            var video = OriginalDataReader.FindCaseInsensitive(Path.Combine(source.DataDirectory, videoName));
            files.Add(await CopyAsync(output, video, Path.Combine(output, "video", videoName + ".smk"),
                $"DATA/{videoName}", "video/x-smacker"));
        }
        foreach (var rawName in new[] { "CLT00002", "DATA.Z" })
        {
            var raw = OriginalDataReader.FindCaseInsensitive(Path.Combine(source.DataDirectory, rawName));
            files.Add(await CopyAsync(output, raw, Path.Combine(output, "raw", "data", rawName),
                $"DATA/{rawName}", "application/octet-stream"));
        }
        foreach (var image in Directory.EnumerateFiles(Path.Combine(source.DataDirectory, "PX08")))
        {
            var name = Path.GetFileName(image).ToUpperInvariant();
            files.Add(await CopyAsync(output, image, Path.Combine(output, "raw", "px08", name),
                $"DATA/PX08/{name}", "application/x-chaos-px08"));
            var px16Image = OriginalDataReader.FindCaseInsensitive(Path.Combine(px16, name));
            if (!PxDimensions.TryGet(name, new FileInfo(px16Image).Length, out var size))
                throw new InvalidDataException($"Dimensions are unknown for PX08 resource {name}.");
            var decodedPath = Path.Combine(output, "images8", name + ".bmp");
            Directory.CreateDirectory(Path.GetDirectoryName(decodedPath)!);
            var px08Bytes = await File.ReadAllBytesAsync(image);
            var sourceCompression = BitConverter.ToInt32(px08Bytes, 30);
            byte[] decoded;
            try
            {
                decoded = Px08BmpDecoder.Decode(px08Bytes, size.Width, size.Height);
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException($"Cannot decode {name}: {exception.Message}", exception);
            }
            await File.WriteAllBytesAsync(decodedPath, decoded);
            files.Add(await DescribeAsync(output, decodedPath, $"DATA/PX08/{name}", "image/bmp",
                new AssetConversion(sourceCompression == 1 ? "rle8-decode" : "bmp-header-repair",
                    size.Width, size.Height, 8, "indexed BGRA palette")));
        }
        Console.WriteLine("Importing help files...");
        if (Directory.Exists(source.HelpDirectory))
        {
            foreach (var help in Directory.EnumerateFiles(source.HelpDirectory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source.HelpDirectory, help).Replace('\\', '/');
                files.Add(await CopyAsync(output, help, Path.Combine(output, "help", relative),
                    $"HELP/{relative}", MediaTypes.ForPath(help)));
            }
            var legacyHelp = OriginalDataReader.FindCaseInsensitive(
                Path.Combine(source.HelpDirectory, "Chaos.hlp"));
            var legacyContents = OriginalDataReader.FindCaseInsensitive(
                Path.Combine(source.HelpDirectory, "CHAOS.CNT"));
            var modernHelp = WinHelpDecoder.Decode(legacyHelp, legacyContents);
            var modernHelpPath = Path.Combine(output, "help", "contents.json");
            await File.WriteAllTextAsync(modernHelpPath,
                JsonSerializer.Serialize(modernHelp, jsonOptions));
            files.Add(await DescribeAsync(output, modernHelpPath,
                "HELP/Chaos.hlp + HELP/CHAOS.CNT", "application/vnd.rechaos.help+json",
                new AssetConversion("winhelp-topic-decode")));
        }

        Console.WriteLine("Writing and verifying the imported asset manifest...");
        var manifest = new AssetManifest(FormatVersion, "Chaos Overlords original asset pack", source.Fingerprint,
            DateTimeOffset.UtcNow, files.OrderBy(file => file.Path).ToArray(),
            typeof(ExtractorProgram).Assembly.GetName().Version?.ToString() ?? "unknown");
        await File.WriteAllTextAsync(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, jsonOptions));
        return manifest;
    }

    private static ExtractorOptions ParseArguments(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help"))
            throw new ArgumentException("Usage:\n  Rechaos.Extractor --source <original install> [--output <assets>] [--force]\n  Rechaos.Extractor --verify-source --source <original install>\n  Rechaos.Extractor --verify-output [--output <assets>] [--quick]\n  Rechaos.Extractor --catalog [--output <assets>] [--catalog-output <markdown>]\n  Rechaos.Extractor --analyze-px [--output <assets>]");
        string? source = null;
        var output = Path.Combine("src", "Rechaos.Game", "Assets");
        var mode = ExtractorMode.Extract;
        var quick = false;
        var force = false;
        string? catalogOutput = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--source" && ++i < args.Length) source = args[i];
            else if (args[i] == "--output" && ++i < args.Length) output = args[i];
            else if (args[i] == "--verify-source") mode = ExtractorMode.VerifySource;
            else if (args[i] == "--verify-output") mode = ExtractorMode.VerifyOutput;
            else if (args[i] == "--quick") quick = true;
            else if (args[i] == "--force") force = true;
            else if (args[i] == "--catalog") mode = ExtractorMode.GenerateCatalog;
            else if (args[i] == "--analyze-px") mode = ExtractorMode.AnalyzePxColor;
            else if (args[i] == "--catalog-output" && ++i < args.Length) catalogOutput = args[i];
            else throw new ArgumentException($"Unknown or incomplete argument: {args[i]}");
        }
        if (quick && mode != ExtractorMode.VerifyOutput)
            throw new ArgumentException("--quick is valid only with --verify-output.");
        if (force && mode != ExtractorMode.Extract)
            throw new ArgumentException("--force is valid only when extracting.");
        if (catalogOutput is not null && mode != ExtractorMode.GenerateCatalog)
            throw new ArgumentException("--catalog-output is valid only with --catalog.");
        return new ExtractorOptions(mode, source, output, quick, force, catalogOutput);
    }

    private static void PrintVerification(AssetPackVerification result, string output)
    {
        if (result.IsValid)
            Console.WriteLine($"Asset pack is valid: {result.VerifiedFiles} files at {Path.GetFullPath(output)}");
        else
            foreach (var error in result.Errors) Console.Error.WriteLine(error);
    }

    private static async Task<ExtractedAsset> CopyAsync(
        string root, string source, string destination, string sourcePath, string mediaType)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
        return await DescribeAsync(root, destination, sourcePath, mediaType, new AssetConversion("copy"));
    }

    private static async Task<ExtractedAsset> DescribeAsync(
        string root, string path, string sourcePath, string mediaType, AssetConversion conversion) =>
        new(Path.GetRelativePath(root, path).Replace('\\', '/'), new FileInfo(path).Length, await HashAsync(path),
            sourcePath, mediaType, conversion);

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    }

    private static PxColorComparison AnalyzePxColor(string output)
    {
        var comparison = new PxColorComparison(0, 0, 0, 0, 0, 0);
        foreach (var indexedPath in Directory.EnumerateFiles(Path.Combine(output, "images8"), "*.bmp")
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var sixteenBitPath = Path.Combine(output, "images", Path.GetFileName(indexedPath));
            if (!File.Exists(sixteenBitPath))
                throw new InvalidDataException($"PX16 counterpart is missing for {Path.GetFileName(indexedPath)}.");
            comparison += PxColorFormatAnalyzer.Compare(File.ReadAllBytes(indexedPath), File.ReadAllBytes(sixteenBitPath));
        }
        return comparison;
    }
}

public enum ExtractorMode { Extract, VerifySource, VerifyOutput, GenerateCatalog, AnalyzePxColor }
public sealed record ExtractorOptions(
    ExtractorMode Mode, string? Source, string Output, bool Quick, bool Force, string? CatalogOutput);
public sealed record OriginalAssetPack(
    string Root, string DataDirectory, string MusicDirectory, string HelpDirectory, string Fingerprint);

public static class SourceFingerprint
{
    public static async Task<string> ComputeAsync(string dataDirectory, string musicDirectory, string helpDirectory)
    {
        var files = new List<(string Relative, string Path)>();
        files.AddRange(Directory.EnumerateFiles(dataDirectory, "*", SearchOption.AllDirectories)
            .Select(path => ($"data/{Path.GetRelativePath(dataDirectory, path).Replace('\\', '/').ToLowerInvariant()}", path)));
        if (Directory.Exists(musicDirectory))
            files.AddRange(Directory.EnumerateFiles(musicDirectory, "*.ogg")
                .Select(path => ($"music/{Path.GetFileName(path).ToLowerInvariant()}", path)));
        if (Directory.Exists(helpDirectory))
            files.AddRange(Directory.EnumerateFiles(helpDirectory, "*", SearchOption.AllDirectories)
                .Select(path => ($"help/{Path.GetRelativePath(helpDirectory, path).Replace('\\', '/').ToLowerInvariant()}", path)));

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files.OrderBy(file => file.Relative, StringComparer.Ordinal))
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(file.Relative));
            hash.AppendData([0]);
            hash.AppendData(BitConverter.GetBytes(new FileInfo(file.Path).Length));
            await using var stream = File.OpenRead(file.Path);
            var buffer = new byte[128 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer)) != 0) hash.AppendData(buffer.AsSpan(0, read));
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

public static class BmpRepair
{
    public static void Repair(byte[] bytes, int width, int height, short bitsPerPixel)
    {
        if (bytes.Length < 54 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
            throw new InvalidDataException("PX resource is not a BMP container.");
        BitConverter.TryWriteBytes(bytes.AsSpan(18, 4), width);
        BitConverter.TryWriteBytes(bytes.AsSpan(22, 4), height);
        BitConverter.TryWriteBytes(bytes.AsSpan(26, 2), (short)1);
        BitConverter.TryWriteBytes(bytes.AsSpan(28, 2), bitsPerPixel);
    }
}

public static class PxDimensions
{
    public readonly record struct Size(int Width, int Height);
    public static bool TryGet(string name, long bytes, out Size size)
    {
        size = name switch
        {
            "PX00100" or "PX00128" or "PX00130" or "PX00131" or "PX00143" or "PX00144" or "PX00145" or "PX00146" => new(640, 460),
            "PX00129" => new(512, 646),
            "PX00132" => new(108, 164),
            "PX00137" or "PX00139" => new(220, 72),
            "PX00138" => new(720, 48),
            _ when bytes == 176_022 => new(312, 282),
            _ when bytes == 24_694 => new(220, 56),
            "PX00200" => new(428, 410),
            "PX00201" => new(320, 240),
            "PX00202" or "PX00203" => new(312, 393),
            "PX00300" => new(324, 64),
            "PX02000" => new(120, 1408),
            "PX03000" => new(640, 576),
            "PX04999" => new(20, 1280),
            _ when name.StartsWith("PX04", StringComparison.Ordinal) && bytes == 69_174 => new(720, 48),
            _ when name.StartsWith("PX05", StringComparison.Ordinal) && bytes == 143_846 => new(344, 209),
            _ when name.StartsWith("PX06", StringComparison.Ordinal) && bytes == 76_526 => new(242, 158),
            _ when name.StartsWith("PX06", StringComparison.Ordinal) && bytes == 76_042 => new(242, 157),
            _ when name.StartsWith("PX07", StringComparison.Ordinal) => new(512, 64),
            _ when name.StartsWith("PX1000", StringComparison.Ordinal) => new(432, 416),
            _ => default
        };
        return size != default;
    }
}
