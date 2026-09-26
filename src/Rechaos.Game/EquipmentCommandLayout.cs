using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class EquipmentCommandLayout
{
    public const int CategoryCount = 4;
    public const int VisibleItemCount = 16;
    public const int EquippedItemCount = 3;
    private const int EquippedItemSize = 20;
    private const int EquippedItemStride = 22;
    private const int EquippedItemGap = 4;
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Portrait => SharedPanelLayout.StandardPortrait;
    public static Rectangle Cancel => SharedPanelLayout.CommandCancel;
    public static Rectangle Ok => SharedPanelLayout.CommandOk;

    public static bool CanConfirm(int selectedIndex, IReadOnlyCollection<int> visibleCategoryIndices)
    {
        ArgumentNullException.ThrowIfNull(visibleCategoryIndices);
        return visibleCategoryIndices.Contains(selectedIndex);
    }

    public static string ResearchProgress(int difficulty, int remaining)
    {
        if (difficulty <= 0) throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (remaining is < 0 || remaining > difficulty)
            throw new ArgumentOutOfRangeException(nameof(remaining));
        return $"{difficulty - remaining}/{difficulty}";
    }

    public static int FirstVisibleItem(int itemCount, int selectedPosition)
    {
        if (itemCount < 0) throw new ArgumentOutOfRangeException(nameof(itemCount));
        if (itemCount == 0) return 0;
        if (selectedPosition is < 0 || selectedPosition >= itemCount)
            throw new ArgumentOutOfRangeException(nameof(selectedPosition));
        return 0;
    }

    /// <summary>
    /// Boxes for the gang's current weapon, armor, and miscellaneous items: a row
    /// spanning the portrait's width directly beneath it, clear of the Cancel button.
    /// </summary>
    public static Rectangle EquippedItem(int slot)
    {
        if (slot is < 0 or >= EquippedItemCount) throw new ArgumentOutOfRangeException(nameof(slot));
        var portrait = Portrait;
        return new Rectangle(portrait.X + slot * EquippedItemStride, portrait.Bottom + EquippedItemGap,
            EquippedItemSize, EquippedItemSize);
    }

    public static Rectangle Category(int category)
    {
        if (category is < 0 or >= CategoryCount) throw new ArgumentOutOfRangeException(nameof(category));
        return SharedPanelLayout.At(103, 16 + category * 36, 34, 34);
    }

    public static Rectangle CategoryHit(int category)
    {
        if (category is < 0 or >= CategoryCount) throw new ArgumentOutOfRangeException(nameof(category));
        return SharedPanelLayout.At(104, 16 + category * 36, 32, 32);
    }

    public static int CategoryForItemType(int itemType) => itemType switch
    {
        0 or 1 => 0,
        2 => 1,
        3 => 2,
        4 => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(itemType))
    };

    public static Rectangle ItemRow(int row)
    {
        if (row is < 0 or >= VisibleItemCount) throw new ArgumentOutOfRangeException(nameof(row));
        return SharedPanelLayout.At(147, 25 + row * 9, 181, 9);
    }

    public static Rectangle ItemListHit => SharedPanelLayout.At(148, 26, 180, 143);

    public static Rectangle ResearchItemListHit => SharedPanelLayout.At(148, 19, 180, 143);

    public static int ItemRowAt(Point point)
    {
        if (!ItemListHit.Contains(point)) return -1;
        return (point.Y - ItemListHit.Y) / 9;
    }

    public static int ResearchItemRowAt(Point point)
    {
        if (!ResearchItemListHit.Contains(point)) return -1;
        return (point.Y - ItemListHit.Y) / 9;
    }
}
