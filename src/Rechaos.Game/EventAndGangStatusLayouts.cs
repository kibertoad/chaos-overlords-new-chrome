using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class GangStatusMarkerPresentation
{
    public static Rectangle Source(bool hasIdleGang) => hasIdleGang
        ? OriginalSpriteLayout.IdleGangStatus
        : OriginalSpriteLayout.AssignedGangStatus;
}

public static class LastTurnEventsLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Page => new(138, 138, 47, 7);
    public static Rectangle Previous => new(135, 151, 25, 21);
    public static Rectangle Next => new(163, 151, 25, 21);
    public static Rectangle Artwork => new(198, 133, 242, 158);
    public static Rectangle DateValue => new(225, 299, 43, 7);
    public static Rectangle ObjectValue => new(305, 299, 135, 7);
    public static Rectangle StatusValue => new(239, 308, 201, 7);
    public static Rectangle ResearchItem => new(296, 187, 48, 48);
    public static Rectangle Delete => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
}
