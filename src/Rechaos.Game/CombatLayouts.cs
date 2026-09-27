using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class CombatPanelLayout
{
    public const int ForceBarHeight = 3;

    /// <summary>
    /// Each gang's two Force tracks in SCR-COMBAT-002: the upper shows <c>force_start</c> and the
    /// lower <c>force_shown</c>, at local y 116 and 123 (FND-COMBAT-009, FND-COMBAT-010).
    /// </summary>
    public const int ForceBarTracks = 2;

    /// <summary>Pixels of a Detailed Combat track per point of Force (FND-COMBAT-010).</summary>
    public const int ForceBarPixelsPerPoint = 6;

    private const int LeftCombatantX = 150;
    private const int RightCombatantX = 223;
    private const int LeftAnimationX = 150;
    private const int RightAnimationX = 223;
    private const int AnimationY = 130;
    private const int ForceBarY = 116;
    private const int ForceBarStride = 7;
    private const int ForceBarWidth = 60;
    private const int LeftItemX = 100;
    private const int RightItemX = 289;
    private const int ItemTop = 48;
    private const int ItemStride = 49;
    private const int ItemSize = 48;

    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Sector => SharedPanelLayout.At(31, 11, 54, 52);
    public static Point SectorCodeText => new(SharedPanelLayout.X(52), SharedPanelLayout.Y(66));
    /// <summary>
    /// The Exit face of SCR-COMBAT-002, local <c>(33,169)-(82,191)</c>, tracked as screen
    /// <c>(137,293)-(187,316)</c> (FND-COMBAT-010).
    /// </summary>
    public static Rectangle Exit => SharedPanelLayout.At(33, 169, 50, 23);

    /// <summary>The pressed Exit face in <c>PX00129</c>, drawn over <see cref="Exit"/> (FND-EVENT-005).</summary>
    public static Rectangle ExitPressedSource => new(50, 386, 50, 23);

    /// <summary>The 60-by-3 red track in <c>PX00129</c> (FND-COMBAT-009).</summary>
    public static Rectangle RedTrackSource => new(354, 3, ForceBarWidth, ForceBarHeight);

    /// <summary>
    /// The first <paramref name="width"/> pixels of the green strip in <c>PX00129</c>, drawn over
    /// the red track from its left edge (FND-COMBAT-009).
    /// </summary>
    public static Rectangle GreenTrackSource(int width) =>
        new(354, 0, Math.Clamp(width, 0, ForceBarWidth), ForceBarHeight);

    /// <summary>The width of the green part of a track for <paramref name="force"/> (FND-COMBAT-009).</summary>
    public static int TrackFill(int force) =>
        Math.Clamp(force * ForceBarPixelsPerPoint, 0, ForceBarWidth);
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

    /// <summary>
    /// SCR-COMBAT-002, FND-COMBAT-014: the 18-by-32 strip filled with the owner's colour at the
    /// left of each gang's header.
    /// </summary>
    public static Rectangle HeaderColor(bool right) => right
        ? SharedPanelLayout.At(224, 13, 18, 32)
        : SharedPanelLayout.At(101, 13, 18, 32);

    /// <summary>
    /// The owner's 32-by-32 Overlord portrait from <c>PX00129</c> row 480, right of the colour
    /// strip (FND-COMBAT-014).
    /// </summary>
    public static Rectangle HeaderPortrait(bool right) => right
        ? SharedPanelLayout.At(242, 13, 32, 32)
        : SharedPanelLayout.At(119, 13, 32, 32);

    /// <summary>Where <c>fn_00413FD5</c> writes the owner's name (FND-COMBAT-014).</summary>
    public static Point HeaderName(bool right) => right
        ? new Point(SharedPanelLayout.X(276), SharedPanelLayout.Y(14))
        : new Point(SharedPanelLayout.X(153), SharedPanelLayout.Y(14));

    /// <summary>
    /// What each side clears in black before its header is drawn (FND-COMBAT-014): the left
    /// side only the 60-by-8 name field, the right side its whole 116-by-36 header.
    /// </summary>
    public static Rectangle HeaderClear(bool right) => right
        ? SharedPanelLayout.At(222, 11, 116, 36)
        : SharedPanelLayout.At(153, 14, 60, 8);

    /// <summary>
    /// The police opponent's header: the 116-by-36 area at <c>(208,0)</c> of <c>PX00300</c>,
    /// drawn over the right header (FND-COMBAT-014).
    /// </summary>
    public static Rectangle PoliceHeader => SharedPanelLayout.At(222, 11, 116, 36);

    public static Rectangle PoliceHeaderSource => new(208, 0, 116, 36);

    /// <summary>The police portrait, the 64-by-64 area at <c>(0,0)</c> of <c>PX00300</c> (FND-COMBAT-014).</summary>
    public static Rectangle PolicePortraitSource => new(0, 0, 64, 64);

    /// <summary>
    /// The police opponent's three item pictures, the 48-by-48 areas at x 64, 112 and 160 of
    /// <c>PX00300</c>, drawn in the right side's item slots (FND-COMBAT-014).
    /// </summary>
    public static Rectangle PoliceItemSource(int slot)
    {
        if (slot is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(64 + 48 * slot, 0, 48, 48);
    }

    public static Rectangle GangPortrait(bool right)
        => SharedPanelLayout.At(right ? RightCombatantX : LeftCombatantX, 48, 64, 64);

    public static Rectangle EquipmentItem(bool right, int slot)
    {
        if (slot is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(
            right ? RightItemX : LeftItemX,
            ItemTop + ItemStride * slot,
            ItemSize,
            ItemSize);
    }

    public static Rectangle ForceBar(bool right, int track)
    {
        if (track is < 0 or >= ForceBarTracks) throw new ArgumentOutOfRangeException(nameof(track));
        return SharedPanelLayout.At((right ? RightCombatantX : LeftCombatantX) + 2,
            ForceBarY + track * ForceBarStride, ForceBarWidth, ForceBarHeight);
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

    /// <summary>Width of a Combat Results Force track (FND-COMBAT-012).</summary>
    public const int ForceTrackWidth = 40;

    /// <summary>Pixels of a Combat Results track per point of Force (FND-COMBAT-012).</summary>
    public const int ForceTrackPixelsPerPoint = 4;

    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Previous => SharedPanelLayout.At(31, 33, 26, 23);
    public static Rectangle Next => SharedPanelLayout.At(59, 33, 26, 23);
    public static Rectangle Sector => SharedPanelLayout.At(31, 67, 54, 52);
    public static Rectangle FriendlyPanel => SharedPanelLayout.At(98, 16, 94, 179);
    public static Rectangle EnemyPanel => SharedPanelLayout.At(240, 16, 94, 179);
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);
    public static Point SectorCodeText => new(SharedPanelLayout.X(52), SharedPanelLayout.Y(122));
    public static Point EmptyText => new(SharedPanelLayout.X(115), SharedPanelLayout.Y(100));

    /// <summary>
    /// The two-cell page number of SCR-COMBAT-001, from <c>(138,137)</c> (FND-COMBAT-007). The
    /// panel art's OF between it and <see cref="PageCount"/> stays as drawn.
    /// </summary>
    public static Rectangle PageNumber => SharedPanelLayout.At(34, 13,
        2 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);

    /// <summary>The two-cell page count of SCR-COMBAT-001, from <c>(174,137)</c> (FND-COMBAT-007).</summary>
    public static Rectangle PageCount => SharedPanelLayout.At(70, 13,
        2 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);

    /// <summary>The Previous arrow in <c>PX00129</c>, greyed on the first page (FND-COMBAT-007).</summary>
    public static Rectangle PreviousSource(bool firstPage) => new(firstPage ? 170 : 118, 363, 26, 23);

    /// <summary>The Next arrow in <c>PX00129</c>, greyed on the last page (FND-COMBAT-007).</summary>
    public static Rectangle NextSource(bool lastPage) => new(lastPage ? 196 : 144, 363, 26, 23);

    /// <summary>
    /// The police strip over the top of the sector tile, drawn when any player's police flag for
    /// the sector is set (FND-COMBAT-007).
    /// </summary>
    public static Rectangle PoliceStrip => SharedPanelLayout.At(31, 67, 54, 9);

    public static Rectangle PoliceStripSource => new(0, 432, 54, 9);

    /// <summary>
    /// A grid cell's Force tracks, 40 by 3 at the cell's origin plus <c>(0,41)</c> for
    /// <c>force_start</c> (track 0) and <c>(0,45)</c> for <c>force_final</c> (track 1)
    /// (FND-COMBAT-012).
    /// </summary>
    public static Rectangle ForceTrack(Rectangle cell, int track)
    {
        if (track is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(track));
        return new Rectangle(cell.X, cell.Y + (track == 0 ? 41 : 45),
            ForceTrackWidth, CombatPanelLayout.ForceBarHeight);
    }

    /// <summary>The width of the green part of a grid track for <paramref name="force"/> (FND-COMBAT-012).</summary>
    public static int ForceTrackFill(int force) =>
        Math.Clamp(force * ForceTrackPixelsPerPoint, 0, ForceTrackWidth);

    /// <summary>
    /// The focus outline around a grid cell, <c>(x - 2, y - 2)-(x + 42, y + 50)</c>
    /// (FND-COMBAT-012).
    /// </summary>
    public static Rectangle FocusOutline(Rectangle cell) => new(cell.X - 2, cell.Y - 2, 44, 52);

    /// <summary>
    /// The art in <c>PX00129</c> copied over <see cref="FocusOutline"/> for a gang that is both the
    /// focal gang's target and one of its attackers (FND-COMBAT-012).
    /// </summary>
    public static Rectangle MutualFocusSource => new(468, 15, 44, 52);

    /// <summary>The 34-by-34 frame one pixel outside the chosen opponent's portrait (FND-COMBAT-009).</summary>
    public static Rectangle OpponentFrame(int slot)
    {
        var portrait = Opponent(slot);
        return new Rectangle(portrait.X - 1, portrait.Y - 1, 34, 34);
    }

    public static Rectangle OpponentFrameSource => new(120, 171, 34, 34);

    /// <summary>
    /// An opponent's 32-by-32 portrait in <c>PX00129</c>: row 480, or the dim row 594 for a player
    /// with no result in the sector (FND-COMBAT-007).
    /// </summary>
    public static Rectangle OpponentPortraitSource(int portraitId, bool hasResult)
    {
        var bright = OriginalSpriteLayout.OverlordPortrait(portraitId);
        return hasResult ? bright : bright with { Y = 594 };
    }

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
