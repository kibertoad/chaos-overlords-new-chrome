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
    public static Rectangle Panel => SharedPanelLayout.Panel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    public static Rectangle Portrait(int slot)
    {
        ValidateSlot(slot);
        return SharedPanelLayout.At(164 + slot * 40, 14, 32, 32);
    }

    public static int StatRight(int slot)
    {
        ValidateSlot(slot);
        return SharedPanelLayout.X(185 + slot * 40);
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
            0 => SharedPanelLayout.Y(48),
            1 => SharedPanelLayout.Y(57),
            2 => SharedPanelLayout.Y(67),
            3 => SharedPanelLayout.Y(76),
            4 => SharedPanelLayout.Y(85),
            5 => SharedPanelLayout.Y(94),
            6 => SharedPanelLayout.Y(104),
            7 => SharedPanelLayout.Y(113),
            8 => SharedPanelLayout.Y(122),
            9 => SharedPanelLayout.Y(131),
            10 => SharedPanelLayout.Y(140),
            11 => SharedPanelLayout.Y(150),
            12 => SharedPanelLayout.Y(159),
            13 => SharedPanelLayout.Y(168),
            14 => SharedPanelLayout.Y(177),
            15 => SharedPanelLayout.Y(186),
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
