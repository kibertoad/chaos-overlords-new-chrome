using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class CombatPanelLayout
{
    public const int ForceBarHeight = 3;

    private const int LeftCombatantX = 150;
    private const int RightCombatantX = 223;
    private const int LeftAnimationX = 150;
    private const int RightAnimationX = 223;
    private const int AnimationY = 130;
    private const int ForceBarY = 114;
    private const int ForceBarWidth = 60;
    private const int LeftItemX = 100;
    private const int RightItemX = 289;
    private const int ItemTop = 48;
    private const int ItemStride = 49;
    private const int ItemSize = 48;

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
        => SharedPanelLayout.At(right ? RightCombatantX : LeftCombatantX, 48, 64, 64);

    public static Rectangle PolicePortrait(bool right)
    {
        var portrait = GangPortrait(right);
        return new Rectangle(portrait.X + 8, portrait.Y, 48, 64);
    }

    public static Rectangle EquipmentItem(bool right, int slot)
    {
        if (slot is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(
            right ? RightItemX : LeftItemX,
            ItemTop + ItemStride * slot,
            ItemSize,
            ItemSize);
    }

    public static Rectangle ForceBar(bool right)
    {
        return SharedPanelLayout.At((right ? RightCombatantX : LeftCombatantX) + 2,
            ForceBarY, ForceBarWidth, ForceBarHeight);
    }
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
    public static Rectangle Previous => SharedPanelLayout.At(31, 33, 26, 23);
    public static Rectangle Next => SharedPanelLayout.At(59, 33, 26, 23);
    public static Rectangle Sector => SharedPanelLayout.At(31, 67, 54, 52);
    public static Rectangle FriendlyPanel => SharedPanelLayout.At(98, 16, 94, 179);
    public static Rectangle EnemyPanel => SharedPanelLayout.At(240, 16, 94, 179);
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);
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

    public static int? FriendlyForceSlotAt(Point point)
    {
        var localX = point.X - SharedPanelLayout.Left;
        var localY = point.Y - SharedPanelLayout.Top;
        if (localX is < 101 or >= 189 || localY is < 27 or >= 183) return null;

        var slot = localX > 144 ? 1 : 0;
        if (localY > 78) slot += 2;
        if (localY > 130) slot += 2;
        return slot;
    }
}
