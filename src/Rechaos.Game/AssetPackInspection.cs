using System.Runtime.ExceptionServices;
using System.Text.Json;
using Rechaos.Core.Assets;

namespace Rechaos.Game;

/// <summary>
/// The one check of an asset pack's manifest. The first-start import reads its state to decide
/// whether to offer an import, and <see cref="ChaosGame"/> throws its problem at startup, so a
/// pack the import leaves alone is one the game opens.
/// </summary>
/// <param name="Problem">
/// Why the pack is not ready: the exception reading the manifest raised, kept as it was so the
/// startup error still names an I/O or JSON failure, or one made here for a missing manifest or
/// another format version. Null when the pack is ready.
/// </param>
public sealed record AssetPackInspection(AssetPackState State, Exception? Problem)
{
    public static AssetPackInspection Of(string assetRoot)
    {
        var manifestPath = Path.Combine(assetRoot, "manifest.json");
        if (!File.Exists(manifestPath))
            return new(AssetPackState.Missing, new FileNotFoundException(
                "Original assets are not installed. Run Rechaos.Extractor with --source pointing at a legal Chaos Overlords installation.",
                manifestPath));
        try
        {
            var manifest = JsonSerializer.Deserialize<AssetManifest>(File.ReadAllText(manifestPath));
            return manifest?.FormatVersion == AssetManifest.CurrentFormatVersion
                ? new(AssetPackState.Ready, null)
                : new(AssetPackState.Incompatible, new InvalidDataException(
                    "The asset pack is incompatible. Run the current extractor again."));
        }
        catch (Exception exception) when (exception is JsonException or IOException
            or UnauthorizedAccessException)
        {
            return new(AssetPackState.Incompatible, exception);
        }
    }

    public void ThrowIfNotReady()
    {
        if (Problem is not null) ExceptionDispatchInfo.Throw(Problem);
    }
}
