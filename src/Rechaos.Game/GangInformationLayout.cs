using Microsoft.Xna.Framework;

namespace Rechaos.Game;

/// <summary>The Gang Information panel of a hired gang or a hire offer (SCR-GANG-002).</summary>
public static class GangInformationLayout
{
    /// <summary>Base values sit 18 pixels left of each effective value (FND-GANG-006, FND-GANG-010).</summary>
    public const int BaseValueOffset = 18;

    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Portrait => EquipmentCommandLayout.Portrait;

    /// <summary>SCR-GANG-002, FND-GANG-006: the close face, panel-local (33,169)-(82,191).</summary>
    public static Rectangle Ok => SharedPanelLayout.At(33, 169, 49, 22);

    // SCR-GANG-002: the name and the three 30-character description rows, from (204,151).
    public static int NameLeft => SharedPanelLayout.X(100);
    public static int NameY => SharedPanelLayout.Y(27);
    public static int DescriptionY(int row) => row switch
    {
        >= 0 and < 3 => SharedPanelLayout.Y(45) + row * OriginalFontLayout.LineHeight,
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    };

    public static int LeftValueLeft => SharedPanelLayout.X(172);
    public static int RightValueLeft => SharedPanelLayout.X(268);
    public static int ForceY => SharedPanelLayout.Y(92);
    public static int TechLevelY => SharedPanelLayout.Y(101);

    // PX05000/PX05022 reserve exactly two opaque glyph cells for each live value.
    public static Rectangle ValueField(int left, int y) => new(
        left, y,
        2 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);

    public static int ValueTextLeft(int left, string text) =>
        left + (2 - text.Length) * OriginalFontLayout.CellWidth;

    /// <summary>
    /// SCR-GANG-002, FND-GANG-006: a carried item's 48-by-48 rotation frame at (392, 141 + 64k).
    /// </summary>
    public static Rectangle Equipment(int slot)
    {
        if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(288, 17 + slot * 64, 48, 48);
    }

    /// <summary>
    /// SCR-GANG-002, FND-GANG-006: the double-click area of an item picture, panel-local
    /// (287, 16 + 64k)-(337, 66 + 64k).
    /// </summary>
    public static Rectangle EquipmentHit(int slot)
    {
        if (slot is < 0 or >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
        return SharedPanelLayout.At(287, 16 + slot * 64, 50, 50);
    }

    public static int? EquipmentSlotAt(Point point)
    {
        for (var slot = 0; slot < 3; slot++)
            if (EquipmentHit(slot).Contains(point)) return slot;
        return null;
    }

    public static int StatisticY(int row) => row switch
    {
        0 => SharedPanelLayout.Y(119),
        1 => SharedPanelLayout.Y(128),
        2 => SharedPanelLayout.Y(146),
        3 => SharedPanelLayout.Y(155),
        4 => SharedPanelLayout.Y(164),
        5 => SharedPanelLayout.Y(173),
        6 => SharedPanelLayout.Y(182),
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    };
}
