using Rechaos.Core.Assets;

namespace Rechaos.Extractor;

public static class AssetPackInstaller
{
    public static async Task<AssetManifest> InstallAsync(
        string output,
        int expectedFormatVersion,
        int expectedFileCount,
        Func<string, Task<AssetManifest>> writeStagedPack)
    {
        ArgumentNullException.ThrowIfNull(writeStagedPack);
        try
        {
            return await InstallUncheckedAsync(output, expectedFormatVersion, expectedFileCount, writeStagedPack);
        }
        // The source was read in full while it was verified, before this runs, so an I/O failure
        // here is the output's: a full disk, a lost permission, a volume that went away.
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or IOException and not FileNotFoundException)
        {
            throw new OutputNotWritableException(output, exception);
        }
    }

    /// <summary>
    /// Creates and removes an empty folder beside <paramref name="output"/>, where the pack is
    /// staged, so a folder the extractor cannot write to is reported before the source is read.
    /// </summary>
    public static void EnsureWritable(string output)
    {
        try
        {
            var (parent, leaf) = Split(output);
            Directory.CreateDirectory(parent);
            var probe = Path.Combine(parent, $".{leaf}.probe-{Guid.NewGuid():N}");
            Directory.CreateDirectory(probe);
            Directory.Delete(probe);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new OutputNotWritableException(output, exception);
        }
    }

    private static (string Parent, string Leaf) Split(string output)
    {
        output = Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Directory.GetParent(output)?.FullName
            ?? throw new ArgumentException("The asset output must be a directory below a filesystem root.", nameof(output));
        var leaf = Path.GetFileName(output);
        if (string.IsNullOrWhiteSpace(leaf))
            throw new ArgumentException("The asset output must name a directory.", nameof(output));
        return (parent, leaf);
    }

    private static async Task<AssetManifest> InstallUncheckedAsync(
        string output,
        int expectedFormatVersion,
        int expectedFileCount,
        Func<string, Task<AssetManifest>> writeStagedPack)
    {
        var (parent, leaf) = Split(output);
        output = Path.Combine(parent, leaf);

        Directory.CreateDirectory(parent);
        var operationId = Guid.NewGuid().ToString("N");
        var staging = Path.Combine(parent, $".{leaf}.staging-{operationId}");
        var backup = Path.Combine(parent, $".{leaf}.backup-{operationId}");
        var oldPackMoved = false;

        try
        {
            Directory.CreateDirectory(staging);
            var manifest = await writeStagedPack(staging);
            var verification = await AssetPackVerifier.VerifyAsync(
                staging, expectedFormatVersion, verifyHashes: true, expectedFileCount);
            if (!verification.IsValid)
                throw new InvalidDataException(
                    "Staged asset pack failed verification: " + string.Join("; ", verification.Errors));

            if (Directory.Exists(output))
            {
                Directory.Move(output, backup);
                oldPackMoved = true;
            }

            try
            {
                Directory.Move(staging, output);
            }
            catch
            {
                if (oldPackMoved && !Directory.Exists(output))
                {
                    Directory.Move(backup, output);
                    oldPackMoved = false;
                }
                throw;
            }

            if (oldPackMoved)
            {
                Directory.Delete(backup, recursive: true);
                oldPackMoved = false;
            }

            return manifest;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            if (oldPackMoved && Directory.Exists(backup) && !Directory.Exists(output))
                Directory.Move(backup, output);
        }
    }
}

/// <summary>The asset pack could not be written to its output folder.</summary>
public sealed class OutputNotWritableException(string output, Exception inner)
    : IOException($"Cannot write the asset pack to {Path.GetFullPath(output)}: {inner.Message}", inner);
