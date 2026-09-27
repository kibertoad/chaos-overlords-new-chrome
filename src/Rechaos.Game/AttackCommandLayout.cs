using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// SCR-ATTACK-001: the Attack picker (Target Acquisition). Every rectangle is panel-local from the
/// origin (104,124) that FND-ATTACK-003 reads from the handler's pointer tests.
/// </summary>
public static class AttackCommandLayout
{
    public const int VisibleTargets = 6;
    public const int OpponentCount = MatchLimits.PlayerCount - 1;
    public const int EquippedItemCount = 3;
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle ActorPortrait => SharedPanelLayout.StandardPortrait;

    /// <summary>
    /// SCR-ATTACK-001, FND-ATTACK-003: the upper face under the portrait is Cancel and the lower
    /// face is Confirm; each is the 50-by-23 face the handler tracks while the button is held.
    /// </summary>
    public static Rectangle Cancel => SharedPanelLayout.At(33, 137, 50, 23);
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 50, 23);

    // FND-UI-019: fn_00418E66 draws the Confirm face from PX00129, (50,386) while the order can be
    // confirmed and (100,386) otherwise. FND-COMLINK-003: the held-button helper fn_00418821
    // shows (50,409) for the Cancel face and (50,386) for the Confirm face while held.
    public static Rectangle OkEnabledSource => new(50, 386, 50, 23);
    public static Rectangle OkDisabledSource => new(100, 386, 50, 23);
    public static Rectangle CancelPressedSource => new(50, 409, 50, 23);
    public static Rectangle OkPressedSource => new(50, 386, 50, 23);

    /// <summary>SCR-ATTACK-001, FND-ATTACK-003: the acting gang's weapon, armor and miscellaneous item.</summary>
    public static Rectangle ActorItem(int slot)
    {
        if (slot is < 0 or >= EquippedItemCount) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(26 + slot * 22, 82, 20, 20);
    }

    /// <summary>SCR-ATTACK-001, FND-ATTACK-001: opponent cell <paramref name="slot"/>, drawn and tested alike.</summary>
    public static Rectangle Opponent(int slot)
    {
        if (slot is < 0 or >= OpponentCount) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(98, 16 + slot * 36, 32, 32);
    }

    /// <summary>SCR-ATTACK-001, FND-ATTACK-002: the frame round the chosen opponent, one pixel outside its cell.</summary>
    public static Rectangle OpponentFrame(int slot)
    {
        if (slot is < 0 or >= OpponentCount) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(97, 15 + slot * 36, 34, 34);
    }

    // SCR-ATTACK-001, FND-ATTACK-002: the two marks are crops of PX00129 keyed on exact white.
    public static Rectangle OpponentFrameSource => new(120, 171, 34, 34);
    public static Rectangle TargetMarkerSource => new(66, 299, 48, 48);

    // SCR-ATTACK-001, FND-ATTACK-001: surface 6 rows 480 (enabled) and 594 (disabled), column by
    // the opponent's portrait number.
    public static Rectangle OpponentSource(int portraitId, bool enabled)
    {
        if (portraitId is < 0 or >= PlayerPortraitLayout.Count)
            throw new ArgumentOutOfRangeException(nameof(portraitId));
        return new Rectangle(portraitId * 32, enabled ? 480 : 594, 32, 32);
    }

    /// <summary>SCR-ATTACK-001, FND-ATTACK-003: the six-cell target area.</summary>
    public static Rectangle TargetArea => SharedPanelLayout.At(135, 16, 202, 177);

    /// <summary>
    /// SCR-ATTACK-001, FND-ATTACK-003: the handler picks cell 3 * (y > 104) + (x > 201) + (x > 269)
    /// from the local point inside <see cref="TargetArea"/>, or -1 outside it.
    /// </summary>
    public static int TargetCellAt(Point point)
    {
        if (!TargetArea.Contains(point)) return -1;
        var x = point.X - SharedPanelLayout.Left;
        var y = point.Y - SharedPanelLayout.Top;
        return 3 * (y > 104 ? 1 : 0) + (x > 201 ? 1 : 0) + (x > 269 ? 1 : 0);
    }

    /// <summary>
    /// Native Attack handler 0x0043b290 partitions one six-cell target region
    /// for pointer selection; its regions are wider than the gang-card art.
    /// </summary>
    public static Rectangle TargetHit(int targetSlot)
    {
        if (targetSlot is < 0 or >= VisibleTargets)
            throw new ArgumentOutOfRangeException(nameof(targetSlot));
        var column = targetSlot % 3;
        var row = targetSlot / 3;
        var x = column switch { 0 => 135, 1 => 202, _ => 270 };
        var width = column switch { 0 => 67, 1 => 68, _ => 67 };
        return SharedPanelLayout.At(x, 16 + row * 89, width, row == 0 ? 89 : 88);
    }

    /// <summary>SCR-ATTACK-001, FND-ATTACK-004: the portrait of target cell <paramref name="targetSlot"/>.</summary>
    public static Rectangle TargetPortrait(int targetSlot)
    {
        if (targetSlot is < 0 or >= VisibleTargets)
            throw new ArgumentOutOfRangeException(nameof(targetSlot));
        return SharedPanelLayout.At(136 + targetSlot % 3 * 68,
            17 + targetSlot / 3 * 90, 64, 64);
    }

    /// <summary>SCR-ATTACK-001, FND-ATTACK-004: a target's item icons, 65 rows below its portrait's top.</summary>
    public static Rectangle TargetItem(int targetSlot, int itemSlot)
    {
        if (itemSlot is < 0 or >= EquippedItemCount) throw new ArgumentOutOfRangeException(nameof(itemSlot));
        var portrait = TargetPortrait(targetSlot);
        return new Rectangle(portrait.X + itemSlot * 22, portrait.Y + 65, 20, 20);
    }

    /// <summary>SCR-ATTACK-001, FND-ATTACK-002: the marker on the chosen target, 8 right of and 2 below the cell's corner.</summary>
    public static Rectangle TargetMarker(int targetSlot)
    {
        if (targetSlot is < 0 or >= VisibleTargets)
            throw new ArgumentOutOfRangeException(nameof(targetSlot));
        return SharedPanelLayout.At(143 + targetSlot % 3 * 68, 18 + targetSlot / 3 * 90, 48, 48);
    }

    /// <summary>
    /// SCR-ATTACK-001, FND-ATTACK-003: Enter (0x0D) and key 0x2B confirm. The rebuild reads 0x2B as
    /// the Execute virtual key, as the other panels do (FND-COMLINK-003, FND-EQUIP-010).
    /// </summary>
    public static IReadOnlyList<Keys> ConfirmKeys { get; } = [Keys.Enter, Keys.Execute];
}
