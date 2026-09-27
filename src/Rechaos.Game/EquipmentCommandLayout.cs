using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class EquipmentCommandLayout
{
    public const int CategoryCount = 4;
    public const int VisibleItemCount = 16;
    public const int EquippedItemCount = 3;
    private const int EquippedItemSize = 20;
    private const int EquippedItemStride = 22;
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
    /// The carried weapon, armor and miscellaneous icons under the portrait, local
    /// (26,82), (48,82) and (70,82), 20 by 20 (SCR-EQUIP-001, FND-EQUIP-010). The same
    /// rectangles open Item Information on a double-click.
    /// </summary>
    public static Rectangle EquippedItem(int slot)
    {
        if (slot is < 0 or >= EquippedItemCount) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(26 + slot * EquippedItemStride, 82, EquippedItemSize, EquippedItemSize);
    }

    /// <summary>
    /// The category frame, one pixel outside the chosen category cell (SCR-EQUIP-001,
    /// FND-EQUIP-009).
    /// </summary>
    public static Rectangle Category(int category)
    {
        if (category is < 0 or >= CategoryCount) throw new ArgumentOutOfRangeException(nameof(category));
        return SharedPanelLayout.At(103, 15 + category * 36, 34, 34);
    }

    /// <summary>PX00129 art of the category frame, keyed on exact white (FND-EQUIP-009).</summary>
    public static Rectangle CategoryFrameSource => new(120, 171, 34, 34);

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

    /// <summary>
    /// The chosen row mark's strip, (251, 149 + 9 * row, 181, 9) (SCR-EQUIP-001,
    /// FND-EQUIP-010). The row's text starts one pixel inside it.
    /// </summary>
    public static Rectangle ItemRow(int row)
    {
        if (row is < 0 or >= VisibleItemCount) throw new ArgumentOutOfRangeException(nameof(row));
        return SharedPanelLayout.At(147, 25 + row * 9, 181, 9);
    }

    public static Point ItemNameOrigin(int row)
    {
        var strip = ItemRow(row);
        return new Point(strip.X + 1, strip.Y + 1);
    }

    /// <summary>
    /// Left edge of the Equip price, a two-cell number field starting at screen x 420
    /// (SCR-EQUIP-001, FND-EQUIP-010) and filled from the right.
    /// </summary>
    public static int PriceLeft(string digits)
    {
        ArgumentNullException.ThrowIfNull(digits);
        if (digits.Length is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(digits));
        return SharedPanelLayout.X(316) + (2 - digits.Length) * OriginalFontLayout.CellWidth;
    }

    public const int ChosenRowCharacters = 30;
    public const int ChosenRowGlyphY = 441;

    /// <summary>
    /// The chosen row's text: the item name padded with spaces to the 30 characters of the
    /// list builder's text rows (FND-EQUIP-010).
    /// </summary>
    public static string ChosenRowText(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Length >= ChosenRowCharacters
            ? name[..ChosenRowCharacters]
            : name.PadRight(ChosenRowCharacters);
    }

    /// <summary>
    /// PX00129 cell of <paramref name="character"/> in the second font row at y 441, which
    /// <c>fn_00414550</c> copies opaquely for the chosen row (FND-EQUIP-010). A character the
    /// strip does not hold is drawn as the space cell.
    /// </summary>
    public static Rectangle ChosenRowGlyphSource(char character)
    {
        if (!OriginalFontLayout.TryGlyph(character, out var glyph)
            || glyph.X >= OriginalFontLayout.AtlasBounds.Width)
            glyph = new Rectangle(0, 0, OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);
        return new Rectangle(glyph.X, ChosenRowGlyphY, glyph.Width, glyph.Height);
    }

    /// <summary>Colour of the chosen row's one-pixel frame (FND-EQUIP-010).</summary>
    public static Color ChosenRowFrame => new(0, 255, 0);

    public static Rectangle ItemListHit => SharedPanelLayout.At(148, 26, 180, 143);

    /// <summary>
    /// SCR-RESEARCH-001, FND-RESEARCH-004: the Research list's double-click rectangle starts seven
    /// pixels above its press rectangle, <see cref="ItemListHit"/>.
    /// </summary>
    public static Rectangle ResearchItemDetailHit => SharedPanelLayout.At(148, 19, 180, 143);

    public static int ItemRowAt(Point point)
    {
        if (!ItemListHit.Contains(point)) return -1;
        return (point.Y - ItemListHit.Y) / 9;
    }

    /// <summary>
    /// The row a double-click opens: rows count from the press rectangle's top and truncate toward
    /// zero, so the seven pixels above the first row open that row.
    /// </summary>
    public static int ResearchItemDetailRowAt(Point point)
    {
        if (!ResearchItemDetailHit.Contains(point)) return -1;
        return (point.Y - ItemListHit.Y) / 9;
    }
}
