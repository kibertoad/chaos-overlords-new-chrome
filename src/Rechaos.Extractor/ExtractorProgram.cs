using System.Security.Cryptography;
using System.Text.Json;
using Rechaos.Core.Assets;

namespace Rechaos.Extractor;

public static class ExtractorProgram
{
    public const int FormatVersion = 2;
    public const int ExpectedExtractedAssetCount = 471;
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

            var source = options.Source
                ?? throw new ArgumentException("--source is required; the port does not distribute original assets.");
            var sourcePack = await VerifySourceAsync(source);
            if (options.Mode == ExtractorMode.VerifySource)
            {
                Console.WriteLine($"Supported original asset pack: {sourcePack.Fingerprint}");
                return 0;
            }

            var existing = await AssetPackVerifier.VerifyAsync(options.Output, FormatVersion,
                expectedFileCount: ExpectedExtractedAssetCount);
            if (existing.IsValid && string.Equals(existing.Manifest?.SourceFingerprintSha256,
                    sourcePack.Fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Asset pack is already complete at {Path.GetFullPath(options.Output)}; extraction skipped.");
                return 0;
            }

            var manifest = await ExtractVerifiedAsync(sourcePack, options.Output);
            var result = await AssetPackVerifier.VerifyAsync(options.Output, FormatVersion,
                expectedFileCount: ExpectedExtractedAssetCount);
            if (!result.IsValid)
                throw new InvalidDataException("Generated asset pack failed verification: " + string.Join("; ", result.Errors));
            Console.WriteLine($"Extracted and verified {manifest.Files.Count} assets to {Path.GetFullPath(options.Output)}");
            return 0;
        }
        catch (ArgumentException exception) { Console.Error.WriteLine(exception.Message); return 2; }
        catch (Exception exception) { Console.Error.WriteLine($"Extraction failed: {exception.Message}"); return 1; }
    }

    public static async Task<AssetManifest> ExtractAsync(string source, string output)
    {
        var sourcePack = await VerifySourceAsync(source);
        return await ExtractVerifiedAsync(sourcePack, output);
    }

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

    private static async Task<AssetManifest> ExtractVerifiedAsync(OriginalAssetPack source, string output)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var files = new List<ExtractedAsset>();
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

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
            files.Add(await DescribeAsync(output, destination));
        }

        foreach (var sound in Directory.EnumerateFiles(source.DataDirectory, "SND*", SearchOption.TopDirectoryOnly))
            files.Add(await CopyAsync(output, sound, Path.Combine(output, "audio", Path.GetFileName(sound).ToUpperInvariant() + ".wav")));
        if (Directory.Exists(source.MusicDirectory))
            foreach (var music in Directory.EnumerateFiles(source.MusicDirectory, "*.ogg"))
                files.Add(await CopyAsync(output, music, Path.Combine(output, "music", Path.GetFileName(music).ToLowerInvariant())));
        foreach (var videoName in new[] { "MVINTRO", "MVLOGOS" })
        {
            var video = OriginalDataReader.FindCaseInsensitive(Path.Combine(source.DataDirectory, videoName));
            files.Add(await CopyAsync(output, video, Path.Combine(output, "video", videoName + ".smk")));
        }
        foreach (var rawName in new[] { "CLT00002", "DATA.Z" })
        {
            var raw = OriginalDataReader.FindCaseInsensitive(Path.Combine(source.DataDirectory, rawName));
            files.Add(await CopyAsync(output, raw, Path.Combine(output, "raw", "data", rawName)));
        }
        foreach (var image in Directory.EnumerateFiles(Path.Combine(source.DataDirectory, "PX08")))
            files.Add(await CopyAsync(output, image, Path.Combine(output, "raw", "px08", Path.GetFileName(image).ToUpperInvariant())));
        if (Directory.Exists(source.HelpDirectory))
            foreach (var help in Directory.EnumerateFiles(source.HelpDirectory, "*", SearchOption.AllDirectories))
                files.Add(await CopyAsync(output, help, Path.Combine(output, "help", Path.GetRelativePath(source.HelpDirectory, help))));

        var manifest = new AssetManifest(FormatVersion, "Chaos Overlords original asset pack", source.Fingerprint,
            DateTimeOffset.UtcNow, files.OrderBy(file => file.Path).ToArray());
        await File.WriteAllTextAsync(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, jsonOptions));
        return manifest;
    }

    private static ExtractorOptions ParseArguments(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help"))
            throw new ArgumentException("Usage:\n  Rechaos.Extractor --source <original install> [--output <assets>]\n  Rechaos.Extractor --verify-source --source <original install>\n  Rechaos.Extractor --verify-output [--output <assets>] [--quick]");
        string? source = null;
        var output = Path.Combine("src", "Rechaos.Game", "Assets");
        var mode = ExtractorMode.Extract;
        var quick = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--source" && ++i < args.Length) source = args[i];
            else if (args[i] == "--output" && ++i < args.Length) output = args[i];
            else if (args[i] == "--verify-source") mode = ExtractorMode.VerifySource;
            else if (args[i] == "--verify-output") mode = ExtractorMode.VerifyOutput;
            else if (args[i] == "--quick") quick = true;
            else throw new ArgumentException($"Unknown or incomplete argument: {args[i]}");
        }
        if (quick && mode != ExtractorMode.VerifyOutput)
            throw new ArgumentException("--quick is valid only with --verify-output.");
        return new ExtractorOptions(mode, source, output, quick);
    }

    private static void PrintVerification(AssetPackVerification result, string output)
    {
        if (result.IsValid)
            Console.WriteLine($"Asset pack is valid: {result.VerifiedFiles} files at {Path.GetFullPath(output)}");
        else
            foreach (var error in result.Errors) Console.Error.WriteLine(error);
    }

    private static async Task<ExtractedAsset> CopyAsync(string root, string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
        return await DescribeAsync(root, destination);
    }

    private static async Task<ExtractedAsset> DescribeAsync(string root, string path) =>
        new(Path.GetRelativePath(root, path).Replace('\\', '/'), new FileInfo(path).Length, await HashAsync(path));

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    }
}

public enum ExtractorMode { Extract, VerifySource, VerifyOutput }
public sealed record ExtractorOptions(ExtractorMode Mode, string? Source, string Output, bool Quick);
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
            "PX00129" => new(512, 646), "PX00132" => new(108, 164),
            "PX00137" or "PX00139" => new(220, 72), "PX00138" => new(720, 48),
            _ when bytes == 176_022 => new(312, 282),
            _ when bytes == 24_694 => new(220, 56),
            "PX00200" => new(428, 410), "PX00201" => new(320, 240), "PX00202" or "PX00203" => new(312, 393),
            "PX00300" => new(324, 64), "PX02000" => new(120, 1408), "PX03000" => new(640, 576),
            "PX04999" => new(20, 1280),
            _ when name.StartsWith("PX04", StringComparison.Ordinal) && bytes == 69_174 => new(720, 48),
            _ when name.StartsWith("PX05", StringComparison.Ordinal) && bytes == 143_846 => new(344, 209),
            _ when name.StartsWith("PX06", StringComparison.Ordinal) && bytes is 76_526 or 76_042 => new(242, 157),
            _ when name.StartsWith("PX07", StringComparison.Ordinal) => new(512, 64),
            _ when name.StartsWith("PX1000", StringComparison.Ordinal) => new(432, 416),
            _ => default
        };
        return size != default;
    }
}
