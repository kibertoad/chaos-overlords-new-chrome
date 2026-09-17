using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class CombatPanelLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Sector => SharedPanelLayout.At(29, 11, 54, 52);
    public static Rectangle Cancel => EquipmentCommandLayout.Ok;
    public static Rectangle LeftHeader => SharedPanelLayout.At(97, 11, 119, 37);
    public static Rectangle RightHeader => SharedPanelLayout.At(220, 11, 119, 37);
    public static Rectangle LeftWeapon => SharedPanelLayout.At(98, 48, 50, 81);
    public static Rectangle LeftGang => SharedPanelLayout.At(149, 48, 67, 81);
    public static Rectangle LeftEquipment => SharedPanelLayout.At(98, 130, 50, 64);
    public static Rectangle LeftAction => SharedPanelLayout.At(149, 130, 67, 64);
    public static Rectangle RightGang => SharedPanelLayout.At(220, 48, 67, 81);
    public static Rectangle RightWeapon => SharedPanelLayout.At(288, 48, 50, 81);
    public static Rectangle RightAction => SharedPanelLayout.At(220, 130, 67, 64);
    public static Rectangle RightEquipment => SharedPanelLayout.At(288, 130, 50, 64);

    public static Rectangle HeaderColor(bool right) => right
        ? SharedPanelLayout.At(223, 14, 12, 28)
        : SharedPanelLayout.At(100, 14, 12, 28);
    public static Rectangle HeaderPortrait(bool right) => right
        ? SharedPanelLayout.At(238, 14, 32, 32)
        : SharedPanelLayout.At(115, 14, 32, 32);
    public static Rectangle ForceBar(bool right) => right
        ? SharedPanelLayout.At(222, 124, 63, 3)
        : SharedPanelLayout.At(151, 124, 63, 3);
}

public static class CombatResultsLayout
{
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Page => SharedPanelLayout.At(29, 11, 58, 12);
    public static Rectangle Previous => SharedPanelLayout.At(31, 36, 25, 20);
    public static Rectangle Next => SharedPanelLayout.At(59, 36, 25, 20);
    public static Rectangle Sector => SharedPanelLayout.At(31, 67, 54, 52);
    public static Rectangle FriendlyPanel => SharedPanelLayout.At(98, 16, 94, 179);
    public static Rectangle EnemyPanel => SharedPanelLayout.At(240, 16, 94, 179);
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public static Rectangle Detail => EquipmentCommandLayout.Cancel;

    public static Rectangle Force(int slot, bool enemy)
    {
        if (slot is < 0 or >= MatchLimits.FriendlyGangsPerSector)
            throw new ArgumentOutOfRangeException(nameof(slot));
        var column = slot % 2;
        var row = slot / 2;
        return SharedPanelLayout.At((enemy ? 246 : 103) + column * 44,
            48 + row * 52, 40, 40);
    }

    public static Rectangle Opponent(int slot)
    {
        if (slot is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(203, 16 + slot * 37, 31, 32);
    }
}
