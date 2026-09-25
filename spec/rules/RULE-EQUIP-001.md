---
id: RULE-EQUIP-001
title: Equip pays the item's price from the cash the player has at that point, and replaces the item in the matching slot
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EQUIP-001, FND-EQUIP-002, FND-EQUIP-006, FND-EVENT-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EQUIP-003, RULE-EVENT-014, FMT-STATE-001, FMT-DATA-003]
---

## Summary

A gang ordered to Equip buys its chosen item if the player has at least the
price in cash when the gang's turn in the transaction pass comes. The item goes
into the weapon, armor or miscellaneous slot, and the item that was there is
lost.

## When it runs

In `transaction_phase`, for a gang whose action is Equip, called by
RULE-EQUIP-002 at the gang's place in the player and roster slot order.

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the gang record, of FMT-STATE-001.

## Inputs

`cash`, `cash_spent`, `item_definitions`, and the gang's `sector` and queued
item (`target`).

## Procedure

```text
let item = gang.target
let price = item_price(player, gang.sector, item)
if cash[player] < price:
    emit EquipCashShort(player, item)
    return
cash[player] = cash[player] - price
cash_spent[player] = cash_spent[player] + price
let kind = item_definitions[item].type
if kind == ITEM_TYPE_ARMOR:
    gang.armor = item
else if kind == ITEM_TYPE_MISC:
    gang.misc = item
else:
    gang.weapon = item
```

## Outputs

No return value. On success, lowers `cash[player]` by the price, raises
`cash_spent[player]` by it, and sets one item slot of the gang. On failure,
changes neither and emits `EquipCashShort`, which records a type-6 Last Turn
report. Makes no random draw.

## Edge cases

- Cash equal to the price is enough. The comparison is signed, so a player
  whose cash is below 0 cannot buy anything.
- The cash counted is what the player has at this gang's turn: Bribes paid in
  `instant_phase`, earlier purchases and earlier Sells have already changed it;
  later Sells, the Chaos payout and the next Upkeep have not.
- Nothing checked cash when the order was given (FND-EQUIP-006), so an order
  the player cannot afford reaches this point and fails here.
- The replaced item is not sold or returned.
- A gift delivered to this gang later in the same pass replaces what it bought
  here (RULE-EQUIP-002).

## What the sources say

SRC-MANUAL-GOG, page 30 (Equip), says Equip buys equipment for the gang, most
items must be researched first, equipping lowers cash by the item's cost, and a
killed gang's equipment is gone forever. Page 39 (Type, Cost, Tech Level) says
a gang carries one weapon, one armor and one miscellaneous item, and can equip
an item only if its Tech Level is at least the item's. Page 23 (Finances) lists
the cost of queued Equips on the Equipment row. The manual does not say when
cash is checked.

## Differences between builds

None known.

## Open questions

- Which field holds the queued item is not recorded; the procedure uses
  `target`.
- The slot for each item `type` (melee, blade and ranged weapons in the weapon
  slot) is taken from the item table's types; FND-EQUIP-002 records only that
  the weapon, armor or miscellaneous byte is replaced, not how the slot is
  chosen.
- FND-EVENT-001 shows that an Equip cash failure records a type-6 report with
  the argument 2, but not the report's other arguments; the event's `item`
  argument is not shown to be among them.
- Whether the resolver checks Tech Level or research again is not recorded; the
  list offers only items that pass them (RULE-EQUIP-004).
