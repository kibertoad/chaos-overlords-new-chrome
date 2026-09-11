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
    // Recreation-internal mechanical queue. The original's separate Comlink
    // message queue retained 16 entries; it did not cap Last Turn Events.
    public const int NotificationsPerPlayer = 64;
}
