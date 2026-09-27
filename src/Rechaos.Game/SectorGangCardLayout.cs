using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class SectorGangCardLayout
{
    public const int Columns = 2;
    public const int Rows = 3;
    public const int VisibleCards = Columns * Rows;
    public const int ItemSlots = 3;
    public const int Left = 254;
    public const int Top = 80;
    public const int ColumnStride = 76;
    public const int RowStride = 112;
    private const int FrameWidth = 74;
    private const int FrameHeight = 110;
    private const int OwnerBorderThickness = 1;
    private const int SelectionInset = 1;
    private const int ContentLeft = 5;
    private const int ForceLeft = 7;
    private const int ForceTop = 3;
    private const int ForceTrackWidth = 60;
    private const int MeterHeight = 3;
    private const int ForcePixelsPerPoint = 6;
    private const int ActionTop = 8;
    private const int ActionWidth = 64;
    private const int ActionHeight = 9;
    private const int OneOffActionWidth = 31;
    private const int RepeatingActionLeft = 37;
    private const int RepeatingActionWidth = 32;
    private const int PortraitTop = 20;
    private const int PortraitSize = 64;
    private const int EquipmentTop = 86;
    private const int EquipmentSize = 20;
    private const int EquipmentStride = 22;

    public static Rectangle Frame(int slot) => At(slot, 0, 0, FrameWidth, FrameHeight);

    /// <summary>
    /// FND-UI-015: a press in <c>(254,80,150,336)</c> takes card
    /// <c>(x &gt; 329) + 2*(y &gt; 192) + 2*(y &gt; 304)</c>, gaps between the cards included; -1 outside.
    /// </summary>
    public static int CardAt(Point point)
    {
        if (!new Rectangle(254, 80, 150, 336).Contains(point)) return -1;
        return (point.X > 329 ? 1 : 0) + 2 * (point.Y > 192 ? 1 : 0) + 2 * (point.Y > 304 ? 1 : 0);
    }

    public static Rectangle OwnerBorder(int slot)
    {
        var frame = Frame(slot);
        return new Rectangle(
            frame.X - OwnerBorderThickness,
            frame.Y - OwnerBorderThickness,
            frame.Width + OwnerBorderThickness * 2,
            frame.Height + OwnerBorderThickness * 2);
    }

    /// <summary>
    /// The band the ctrl-pick highlight paints. It sits inside the card's own bezel, so a picked
    /// card reads as picked without covering the portrait or hiding whose gang it is.
    /// </summary>
    public static Rectangle SelectionBorder(int slot) => At(slot, SelectionInset, SelectionInset,
        FrameWidth - SelectionInset * 2, FrameHeight - SelectionInset * 2);

    public static Rectangle ForceBar(int slot) =>
        At(slot, ForceLeft, ForceTop, ForceTrackWidth, MeterHeight);

    public static int ForceWidth(int force) =>
        Math.Clamp(force, 0, ManualRules.MaximumForce) * ForcePixelsPerPoint;

    public static Rectangle ActionStrip(int slot) =>
        At(slot, ContentLeft, ActionTop, ActionWidth, ActionHeight);

    public static Rectangle OneOffAction(int slot) =>
        At(slot, ContentLeft, ActionTop, OneOffActionWidth, ActionHeight);

    public static Rectangle RepeatingAction(int slot) =>
        At(slot, RepeatingActionLeft, ActionTop, RepeatingActionWidth, ActionHeight);

    public static Rectangle AssignedCommand(int slot) => ActionStrip(slot);

    public static bool? ActionRepeatAt(int slot, Point point) =>
        OneOffAction(slot).Contains(point) ? false :
        RepeatingAction(slot).Contains(point) ? true : null;

    public static Rectangle Portrait(int slot) =>
        At(slot, ContentLeft, PortraitTop, PortraitSize, PortraitSize);

    /// <summary>
    /// The double-click key of a region of a gang's card: 0 for the gang itself, 1 to 3 for an
    /// equipment slot. Every card path registers under it, so two targets never share a key.
    /// </summary>
    public static int ClickKey(GangId gang, int region)
    {
        if (region is < 0 or > ItemSlots) throw new ArgumentOutOfRangeException(nameof(region));
        return gang.Value * (ItemSlots + 1) + region;
    }

    /// <summary>The equipment slot of a card under the point, or -1.</summary>
    public static int ItemSlotAt(int slot, Point point)
    {
        for (var itemSlot = 0; itemSlot < ItemSlots; itemSlot++)
            if (ItemSlot(slot, itemSlot).Contains(point))
                return itemSlot;
        return -1;
    }

    public static Rectangle ItemSlot(int slot, int itemSlot)
    {
        if (itemSlot is < 0 or >= ItemSlots) throw new ArgumentOutOfRangeException(nameof(itemSlot));
        return At(slot, ContentLeft + itemSlot * EquipmentStride,
            EquipmentTop, EquipmentSize, EquipmentSize);
    }

    public static Rectangle ItemPortrait(int slot, int itemSlot) => ItemSlot(slot, itemSlot);

    private static Rectangle At(int slot, int x, int y, int width, int height)
    {
        if (slot is < 0 or >= VisibleCards) throw new ArgumentOutOfRangeException(nameof(slot));
        return new Rectangle(
            Left + slot % Columns * ColumnStride + x,
            Top + slot / Columns * RowStride + y,
            width,
            height);
    }
}
