using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class CombatPanelLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Sector => new(134, 136, 52, 52);
    public static Rectangle Cancel => EquipmentCommandLayout.Ok;
    public static Rectangle LeftHeader => new(201, 136, 119, 37);
    public static Rectangle RightHeader => new(324, 136, 119, 37);
    public static Rectangle LeftWeapon => new(202, 173, 50, 81);
    public static Rectangle LeftGang => new(253, 173, 67, 81);
    public static Rectangle LeftEquipment => new(202, 255, 50, 64);
    public static Rectangle LeftAction => new(253, 255, 67, 64);
    public static Rectangle RightGang => new(324, 173, 67, 81);
    public static Rectangle RightWeapon => new(392, 173, 50, 81);
    public static Rectangle RightAction => new(324, 255, 67, 64);
    public static Rectangle RightEquipment => new(392, 255, 50, 64);

    public static Rectangle HeaderColor(bool right) => right
        ? new Rectangle(327, 139, 12, 28)
        : new Rectangle(204, 139, 12, 28);
    public static Rectangle HeaderPortrait(bool right) => right
        ? new Rectangle(342, 139, 32, 32)
        : new Rectangle(219, 139, 32, 32);
    public static Rectangle ForceBar(bool right) => right
        ? new Rectangle(326, 249, 63, 3)
        : new Rectangle(255, 249, 63, 3);
}

public static class CombatResultsLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Page => new(133, 136, 58, 12);
    public static Rectangle Previous => new(135, 161, 25, 20);
    public static Rectangle Next => new(163, 161, 25, 20);
    public static Rectangle Sector => new(134, 192, 52, 52);
    public static Rectangle FriendlyPanel => new(202, 141, 94, 179);
    public static Rectangle EnemyPanel => new(344, 141, 94, 179);
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public static Rectangle Detail => EquipmentCommandLayout.Cancel;

    public static Rectangle Force(int slot, bool enemy)
    {
        if (slot is < 0 or >= MatchLimits.FriendlyGangsPerSector)
            throw new ArgumentOutOfRangeException(nameof(slot));
        var column = slot % 2;
        var row = slot / 2;
        return new Rectangle((enemy ? 350 : 207) + column * 44, 173 + row * 52, 40, 40);
    }

    public static Rectangle Opponent(int slot)
    {
        if (slot is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(306, 142 + slot * 37, 32, 32);
    }
}
