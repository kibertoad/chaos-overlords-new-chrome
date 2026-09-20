namespace Rechaos.Game;

public static class AssetRootResolver
{
    public const string ApplicationDataDirectory = "ChaosOverlordsNewChrome";

    /// <summary>
    /// The asset pack beside the executable when there is one, otherwise the per-user pack.
    /// </summary>
    /// <remarks>
    /// An adjacent directory only counts when it holds a manifest. Probing for the directory alone
    /// meant a leftover or half-deleted <c>Assets</c> folder hid a complete pack in LocalAppData,
    /// and the game reported that no assets were installed.
    /// </remarks>
    public static string Resolve(string applicationDirectory, string localApplicationData) =>
        Resolve(applicationDirectory, localApplicationData,
            File.Exists(Path.Combine(applicationDirectory, "Assets", "manifest.json")));

    public static string Resolve(
        string applicationDirectory,
        string localApplicationData,
        bool adjacentAssetsExist)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);

        return adjacentAssetsExist
            ? Path.Combine(applicationDirectory, "Assets")
            : Path.Combine(localApplicationData, ApplicationDataDirectory, "Assets");
    }
}
