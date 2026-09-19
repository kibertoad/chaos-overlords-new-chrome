using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class HireReservationWarning
{
    public static int ProjectedBalanceAtHire(int currentCash, FinanceProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return checked(currentCash + projection.Equipment + projection.CityOfficials
            + projection.ChaosEstimate + projection.NewContracts);
    }

    public static string For(int projectedCash) => projectedCash < 0
        ? CityStatusMessage.RequireFit($"HIRE SHORTFALL: ${-(long)projectedCash}")
        : string.Empty;
}

public static class HireComparisonLayout
{
    public static Rectangle Panel => new(128, 124, 320, 209);
    public static Rectangle BackgroundSource => new(0, 0, 320, 209);
    public static Rectangle Ok => new(161, 293, 49, 22);

    public static Rectangle Portrait(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(Panel.X + 164 + slot * 40, Panel.Y + 14, 32, 32);
    }

    public static int StatRight(int slot)
    {
        ValidateSlot(slot);
        return Panel.X + 186 + slot * 40;
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
            0 => Panel.Y + 48,
            1 => Panel.Y + 57,
            2 => Panel.Y + 67,
            3 => Panel.Y + 76,
            4 => Panel.Y + 85,
            5 => Panel.Y + 94,
            6 => Panel.Y + 104,
            7 => Panel.Y + 113,
            8 => Panel.Y + 122,
            9 => Panel.Y + 131,
            10 => Panel.Y + 140,
            11 => Panel.Y + 150,
            12 => Panel.Y + 159,
            13 => Panel.Y + 168,
            14 => Panel.Y + 177,
            15 => Panel.Y + 186,
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
