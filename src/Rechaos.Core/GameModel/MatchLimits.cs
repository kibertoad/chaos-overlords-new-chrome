namespace Rechaos.Core.GameModel;

/// <summary>Structural capacities recovered from the original data and manual.</summary>
public static class MatchLimits
{
    public const int BoardWidth = 8;
    public const int SectorCount = BoardWidth * BoardWidth;
    public const int SitesPerSector = 3;
    public const int SiteCount = SectorCount * SitesPerSector;
    public const int PlayerCount = 6;
    public const int GangsPerPlayer = 80;
    public const int FriendlyGangsPerSector = 6;
    public const int ItemSlots = 64;
    public const int HireOffersPerPlayer = 3;
    public const int ComlinkMessagesPerPlayer = 16;
    public const int ComlinkMessageCharacters = 160;
    public const int LastTurnReportsPerPlayer = 32;
    // Recreation-internal mechanical queue. The original's separate Comlink
    // message queue retained 16 entries and its Last Turn table retained the
    // first 32 reportable records; neither is this richer internal queue.
    public const int NotificationsPerPlayer = 64;

    /// <summary>Whether <paramref name="id"/> names a sector on the board.</summary>
    public static bool IsSectorId(int id) => id is >= 0 and < SectorCount;
}
