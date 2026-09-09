namespace Rechaos.Game;

public static class AssetRootResolver
{
    public const string ApplicationDataDirectory = "ChaosOverlordsNewChrome";

    public static string Resolve(string applicationDirectory, string localApplicationData) =>
        Resolve(applicationDirectory, localApplicationData,
            Directory.Exists(Path.Combine(applicationDirectory, "Assets")));

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
