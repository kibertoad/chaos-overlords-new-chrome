namespace Rechaos.Multiplayer.Session;

/// <summary>
/// What the players are told when somebody starts or stops watching their match.
/// </summary>
/// <remarks>
/// One wording for the lobby chat and the city's message line, upper-cased because the game's font
/// has capitals only.
/// </remarks>
public static class SpectatorAnnouncement
{
    /// <summary>The stand-in for a spectator whose arrival this client never read.</summary>
    public const string UnknownName = "A SPECTATOR";

    public static string Joined(string displayName) => $"{Name(displayName)} IS WATCHING";

    public static string Left(string displayName, bool removed) => removed
        ? $"{Name(displayName)} WAS REMOVED FROM THE SPECTATORS"
        : $"{Name(displayName)} STOPPED WATCHING";

    private static string Name(string displayName)
    {
        var name = displayName.Trim().ToUpperInvariant();
        return name.Length > 0 ? name : UnknownName;
    }
}
