using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class HireComparisonLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    public static Rectangle Portrait(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(268 + slot * 40, 139, 32, 32);
    }

    public static int StatRight(int slot)
    {
        ValidateSlot(slot);
        return 289 + slot * 40;
    }

    public static Rectangle ValueCell(int slot, int row) =>
        new(StatRight(slot) - 12, StatY(row), 12, 7);

    public static string FormatValue(int row, short value)
    {
        if (row is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(row));
        return row == 0 ? value.ToString("D2") : value.ToString();
    }

    public static int StatY(int row)
    {
        if (row is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(row));
        return row switch
        {
            0 => 173,
            1 => 182,
            2 => 192,
            3 => 201,
            4 => 210,
            5 => 219,
            6 => 229,
            7 => 238,
            8 => 247,
            9 => 256,
            10 => 265,
            11 => 275,
            12 => 284,
            13 => 293,
            14 => 302,
            15 => 311,
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
    }

    public static bool IsBestValue(int row, short value, IEnumerable<short> comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        if (row is < 0 or >= 16) throw new ArgumentOutOfRangeException(nameof(row));
        var values = comparison.ToArray();
        if (values.Length == 0)
            throw new ArgumentException("At least one value is required.", nameof(comparison));
        return row == 1 ? value == values.Min() : value == values.Max();
    }

    private static void ValidateSlot(int slot)
    {
        if (slot is < 0 or >= HireDockLayout.SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
    }
}
