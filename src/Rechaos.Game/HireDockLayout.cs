using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>The planning action a dock slot currently carries, drawn over its portrait.</summary>
public enum HireDockMark
{
    None,
    Hired,
    Snubbed
}

public sealed record HireDockEntry(short GangDefinitionId, HireDockMark Mark)
{
    public bool Hired => Mark == HireDockMark.Hired;
}

public static class HireDockLayout
{
    public const int SlotCount = 3;

    public static Rectangle Cell(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(438 + slot * 66, 370, 66, 90);
    }

    /// <summary>SCR-HIRE-002, FND-HIRE-008: the offer portrait, with its hire or snub mark over it.</summary>
    public static Rectangle Portrait(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(440 + slot * 66, 373, 64, 64);
    }

    /// <summary>
    /// SCR-HIRE-002, FND-HIRE-008: the press region of an offer, y 373 to 436 and x 440 to 504,
    /// above 504 to 570 and above 570 to 636, so the three regions meet.
    /// </summary>
    public static Rectangle PortraitHit(int slot)
    {
        ValidateSlot(slot);
        return slot == 0
            ? new Rectangle(440, 373, 65, 64)
            : new Rectangle(439 + slot * 66, 373, 66, 64);
    }

    public static Rectangle Reject(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(472 + slot * 66, 437, 32, 13);
    }

    public static Rectangle PriceCell(int slot)
    {
        ValidateSlot(slot);
        return new Rectangle(438 + slot * 66, 436, 33, 24);
    }

    public static Point Price(int slot)
    {
        var cell = PriceCell(slot);
        const int twoGlyphWidth = 11; // 5px glyph + 1px advance + 5px glyph.
        return new Point(
            cell.X + (cell.Width - twoGlyphWidth) / 2,
            cell.Y + 4);
    }

    public static string PriceText(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        return amount < 10 ? $"0{amount}" : amount.ToString();
    }

    public static IReadOnlyList<HireDockEntry?> Project(
        IReadOnlyList<HireOfferSlotState> offers,
        PendingHireState? pending,
        int? snubbedSlot)
    {
        ArgumentNullException.ThrowIfNull(offers);
        if (offers.Count != SlotCount)
            throw new ArgumentException("Hire dock requires exactly three offer slots.", nameof(offers));
        var result = new HireDockEntry?[SlotCount];
        for (var slot = 0; slot < SlotCount; slot++)
            if (offers[slot].GangDefinitionId is { } definitionId)
                result[slot] = new HireDockEntry(definitionId, MarkFor(slot, pending, snubbedSlot));
        return result;
    }

    public static int MoveCursor(
        IReadOnlyList<HireOfferSlotState> offers,
        int currentSlot,
        int delta)
    {
        ArgumentNullException.ThrowIfNull(offers);
        if (offers.Count != SlotCount)
            throw new ArgumentException("Hire dock requires exactly three offer slots.", nameof(offers));

        var available = Enumerable.Range(0, SlotCount)
            .Where(slot => offers[slot].GangDefinitionId.HasValue)
            .ToArray();
        if (available.Length == 0) return -1;

        var index = Array.IndexOf(available, currentSlot);
        if (index < 0) return delta < 0 ? available[^1] : available[0];
        return available[(index + delta % available.Length + available.Length) % available.Length];
    }

    // Hire and snub are mutually exclusive selections, so at most one slot carries either mark.
    private static HireDockMark MarkFor(int slot, PendingHireState? pending, int? snubbedSlot) =>
        pending?.OfferSlot == slot ? HireDockMark.Hired
        : snubbedSlot == slot ? HireDockMark.Snubbed
        : HireDockMark.None;

    private static void ValidateSlot(int slot)
    {
        if (slot is < 0 or >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
    }
}
