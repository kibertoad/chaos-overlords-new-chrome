using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class GangStatusMarkerPresentation
{
    public static Rectangle Source(
        bool hasIdleGang,
        bool hasDetectedEnemyGang,
        bool hasPendingHire)
    {
        var state = (hasDetectedEnemyGang ? 1 : 0)
            + (hasIdleGang ? 2 : 0)
            + (hasPendingHire ? 4 : 0);
        return OriginalSpriteLayout.GangStatus(state);
    }
}

public static class LastTurnEventsLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Page => SharedPanelLayout.At(34, 13, 47, 7);
    public static Rectangle Previous => SharedPanelLayout.At(31, 33, 26, 23);
    public static Rectangle Next => SharedPanelLayout.At(59, 33, 26, 23);
    public static Rectangle Artwork => SharedPanelLayout.At(94, 8, 242, 158);
    public static Rectangle DateValue => SharedPanelLayout.At(121, 174, 43, 7);
    public static Rectangle ObjectValue => SharedPanelLayout.At(201, 174, 135, 7);
    public static Rectangle StatusValue => SharedPanelLayout.At(135, 183, 201, 7);
    public static Rectangle ResearchItem => SharedPanelLayout.At(192, 62, 48, 48);
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);
}
