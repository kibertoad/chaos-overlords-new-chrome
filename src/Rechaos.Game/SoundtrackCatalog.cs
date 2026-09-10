namespace Rechaos.Game;

public static class SoundtrackCatalog
{
    public static IReadOnlyList<string> ExpectedFileNames { get; } =
        Enumerable.Range(2, 8).Select(track => $"track{track:00}.ogg").ToArray();

    public static IReadOnlyList<string> FindAvailableTracks(string assetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        var musicRoot = Path.Combine(assetRoot, "music");
        return ExpectedFileNames
            .Select(fileName => Path.Combine(musicRoot, fileName))
            .Where(File.Exists)
            .ToArray();
    }
}
