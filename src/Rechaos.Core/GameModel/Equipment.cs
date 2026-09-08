using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public enum EquipmentSlot : byte
{
    Weapon,
    Armor,
    Miscellaneous
}

public static class EquipmentRules
{
    public static EquipmentSlot SlotFor(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Type switch
        {
            0 or 1 or 2 => EquipmentSlot.Weapon,
            3 => EquipmentSlot.Armor,
            4 => EquipmentSlot.Miscellaneous,
            _ => throw new ArgumentOutOfRangeException(nameof(item), item.Type, "Unknown equipment type.")
        };
    }

    public static short? EquippedItem(MatchGangState gang, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(gang);
        return slot switch
        {
            EquipmentSlot.Weapon => gang.WeaponItemId,
            EquipmentSlot.Armor => gang.ArmorItemId,
            EquipmentSlot.Miscellaneous => gang.MiscellaneousItemId,
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }

    internal static short? Equip(MatchGangState gang, EquipmentSlot slot, short itemIndex)
    {
        var replaced = EquippedItem(gang, slot);
        switch (slot)
        {
            case EquipmentSlot.Weapon: gang.WeaponItemId = itemIndex; break;
            case EquipmentSlot.Armor: gang.ArmorItemId = itemIndex; break;
            case EquipmentSlot.Miscellaneous: gang.MiscellaneousItemId = itemIndex; break;
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
        return replaced;
    }

    internal static void Unequip(MatchGangState gang, EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Weapon: gang.WeaponItemId = null; break;
            case EquipmentSlot.Armor: gang.ArmorItemId = null; break;
            case EquipmentSlot.Miscellaneous: gang.MiscellaneousItemId = null; break;
            default: throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    public static int SaleValue(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Cost < 0) throw new ArgumentOutOfRangeException(nameof(item));
        return item.Cost / 2;
    }
}
