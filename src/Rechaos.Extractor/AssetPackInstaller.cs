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

        output = Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Directory.GetParent(output)?.FullName
            ?? throw new ArgumentException("The asset output must be a directory below a filesystem root.", nameof(output));
        var leaf = Path.GetFileName(output);
        if (string.IsNullOrWhiteSpace(leaf))
            throw new ArgumentException("The asset output must name a directory.", nameof(output));

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
