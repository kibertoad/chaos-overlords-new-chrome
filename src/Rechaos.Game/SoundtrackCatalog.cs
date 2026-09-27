namespace Rechaos.Game;

public enum OriginalSoundtrackMode
{
    Title,
    Gameplay,
    Endgame
}

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

public static class OriginalSoundtrackPolicy
{
    public const int MinimumVolumeLevel = 0;
    public const int MaximumVolumeLevel = 10;
    public const int DefaultVolumeLevel = 5;
    private const int OriginalVolumeStep = 25 * 256;

    public static IReadOnlyList<string> FileNamesFor(OriginalSoundtrackMode mode) => mode switch
    {
        OriginalSoundtrackMode.Title => SoundtrackCatalog.ExpectedFileNames.Take(1).ToArray(),
        OriginalSoundtrackMode.Gameplay => SoundtrackCatalog.ExpectedFileNames.Skip(1).Take(6).ToArray(),
        OriginalSoundtrackMode.Endgame => SoundtrackCatalog.ExpectedFileNames.TakeLast(1).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    /// <param name="eliminationMusicHeld">
    /// Whether an elimination card left the endgame music on (RULE-OBJECTIVE-005): the card starts
    /// it, and only a later local human in slot order asks for the gameplay music again.
    /// </param>
    public static OriginalSoundtrackMode ModeFor(ClientScreen screen, bool eliminationMusicHeld = false) =>
        screen switch
        {
            ClientScreen.Title or ClientScreen.Setup => OriginalSoundtrackMode.Title,
            ClientScreen.Endgame or ClientScreen.Elimination => OriginalSoundtrackMode.Endgame,
            _ => eliminationMusicHeld ? OriginalSoundtrackMode.Endgame : OriginalSoundtrackMode.Gameplay
        };

    public static float VolumeForLevel(int level)
    {
        if (level is < MinimumVolumeLevel or > MaximumVolumeLevel)
            throw new ArgumentOutOfRangeException(nameof(level));
        return level * OriginalVolumeStep / (float)ushort.MaxValue;
    }
}
