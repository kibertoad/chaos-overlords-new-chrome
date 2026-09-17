using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class CombatPanelLayout
{
    public const int ForceBarHeight = 3;

    private const int LeftAnimationX = 150;
    private const int RightAnimationX = 223;
    private const int AnimationY = 130;

    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Sector => SharedPanelLayout.At(31, 11, 54, 52);
    public static Point SectorCodeText => new(SharedPanelLayout.X(52), SharedPanelLayout.Y(66));
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
    public static Rectangle Animation(bool right) => right
        ? SharedPanelLayout.At(RightAnimationX, AnimationY, CombatAnimationRouting.FrameSize,
            CombatAnimationRouting.FrameSize)
        : SharedPanelLayout.At(LeftAnimationX, AnimationY, CombatAnimationRouting.FrameSize,
            CombatAnimationRouting.FrameSize);

    public static Rectangle HeaderColor(bool right) => right
        ? SharedPanelLayout.At(223, 14, 12, 28)
        : SharedPanelLayout.At(100, 14, 12, 28);
    public static Rectangle HeaderPortrait(bool right) => right
        ? SharedPanelLayout.At(238, 14, 32, 32)
        : SharedPanelLayout.At(115, 14, 32, 32);
    public static Point HeaderName(bool right)
    {
        var header = right ? RightHeader : LeftHeader;
        return new Point(header.X + 52, header.Y + 4);
    }

    public static Point PoliceName(bool right)
    {
        var header = right ? RightHeader : LeftHeader;
        return new Point(header.X + 8, header.Y + 4);
    }

    public static Rectangle GangPortrait(bool right)
    {
        var cell = right ? RightGang : LeftGang;
        return new Rectangle(cell.X + 1, cell.Y + 1, 64, 64);
    }

    public static Rectangle PolicePortrait(bool right)
    {
        var cell = right ? RightGang : LeftGang;
        return new Rectangle(cell.X + 8, cell.Y + 8, 48, 64);
    }

    public static Point WeaponItem(bool right)
    {
        var cell = right ? RightWeapon : LeftWeapon;
        return new Point(cell.Center.X, cell.Y + 27);
    }

    public static Point ArmorItem(bool right)
    {
        var cell = right ? RightEquipment : LeftEquipment;
        return new Point(cell.Center.X, cell.Y + 13);
    }

    public static Point MiscellaneousItem(bool right)
    {
        var cell = right ? RightEquipment : LeftEquipment;
        return new Point(cell.Center.X, cell.Y + 40);
    }

    public static Rectangle ForceBar(bool right) => right
        ? SharedPanelLayout.At(222, 124, 63, ForceBarHeight)
        : SharedPanelLayout.At(151, 124, 63, ForceBarHeight);
}

public static class CombatResultsLayout
{
    public const int OpponentSlots = MatchLimits.PlayerCount - 1;

    private const int FriendlyForceX = 103;
    private const int EnemyForceX = 246;
    private const int ForceY = 29;
    private const int ForceSize = 40;
    private const int ForceColumnStride = 44;
    private const int ForceRowStride = 52;
    private const int OpponentX = 202;
    private const int OpponentY = 16;
    private const int OpponentSize = 32;
    private const int OpponentStride = 36;

    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Page => SharedPanelLayout.At(29, 11, 58, 12);
    public static Rectangle Previous => SharedPanelLayout.At(31, 36, 25, 20);
    public static Rectangle Next => SharedPanelLayout.At(59, 36, 25, 20);
    public static Rectangle Sector => SharedPanelLayout.At(31, 67, 54, 52);
    public static Rectangle FriendlyPanel => SharedPanelLayout.At(98, 16, 94, 179);
    public static Rectangle EnemyPanel => SharedPanelLayout.At(240, 16, 94, 179);
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
    public static Rectangle Detail => EquipmentCommandLayout.Cancel;
    public static Point PageText => new(SharedPanelLayout.X(34), SharedPanelLayout.Y(13));
    public static Point SectorCodeText => new(SharedPanelLayout.X(52), SharedPanelLayout.Y(122));
    public static Point EmptyText => new(SharedPanelLayout.X(115), SharedPanelLayout.Y(100));

    public static Rectangle ForceBar(Rectangle force) =>
        new(force.X, force.Bottom - CombatPanelLayout.ForceBarHeight,
            force.Width, CombatPanelLayout.ForceBarHeight);

    public static Rectangle Force(int slot, bool enemy)
    {
        if (slot is < 0 or >= MatchLimits.FriendlyGangsPerSector)
            throw new ArgumentOutOfRangeException(nameof(slot));
        var column = slot % 2;
        var row = slot / 2;
        return SharedPanelLayout.At((enemy ? EnemyForceX : FriendlyForceX)
            + column * ForceColumnStride, ForceY + row * ForceRowStride,
            ForceSize, ForceSize);
    }

    public static Rectangle Opponent(int slot)
    {
        if (slot is < 0 or >= OpponentSlots) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(OpponentX, OpponentY + slot * OpponentStride,
            OpponentSize, OpponentSize);
    }
}
