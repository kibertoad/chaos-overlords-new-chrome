---
id: FND-EQUIP-002
title: The transaction pass visits gangs by player and roster slot, holds gifts until the player's scan ends, and pays a multi-item Sell once
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00474BF3
tool: Ghidra 12.1.3
environment: null
---

## Observation

The transaction pass of the whole-turn resolver `fn_00472775` loops over player
slots 0 to 5 and, inside each player, over all 81 gang records in ascending
order.

- Before a player's gang scan it sets every entry of three 81-entry pending
  arrays, one each for weapon, armor and miscellaneous items, to -1.
- For action 5 (Equip) it checks and subtracts cash at once (FND-EQUIP-006)
  and then replaces the weapon, armor or miscellaneous byte of the copied gang
  record.
- For action 6 (Give) it writes each selected item byte into the matching
  pending array at the recipient's roster slot and clears the giver's copied
  item bytes.
- Only after every gang of that player has been handled does it copy each
  pending value other than -1 into the recipient's matching item byte.
- For action 12 (Sell) it tests the bits of a fixed selection mask in the order
  weapon, armor, miscellaneous. Each selected branch clears that copied item
  byte and assigns `Cost / 2` of that item to the same local value. The cash
  and cash-earned updates happen once, after all three branches, with that
  value; the credit to cash is at `0x00474BF3`. The branches do not add to the
  value.

## Interpretation

Transactions are carried out by player slot and then roster slot, whatever
order the orders were given in. A gift reaches its recipient only after the
recipient's own transaction, so it replaces an item the recipient bought this
turn or already had in that slot, and when two gangs give to the same
recipient slot the later roster slot's gift wins. A Sell of several items
removes every selected item but pays only half the Cost of the last selected
slot in the order weapon, armor, miscellaneous: the miscellaneous item if it
was selected, else the armor, else the weapon.

## Alternatives

- Which bits of which field hold the Sell and Give selections, and which field
  holds the Give recipient, are not recorded.
- Whether the scan skips inactive records (sector 100), such as gangs that died
  in this turn's combat, has not been recorded.
- The initial value of the Sell value before the three branches has not been
  recorded; a Sell order always selects at least one item (FND-EQUIP-004), so
  it is always overwritten.
- `Cost / 2` is taken from the item's listed Cost, not from a Factory price.

## How to reproduce

In `fn_00472775`, after the combat damage has been applied, find the player loop
that fills three 81-entry local arrays with -1 and then scans the roster,
dispatching on actions 5, 6 and 12. The Sell case's three bit tests store into
one local that is added to cash at `0x00474BF3`; the arrays are copied into the
recipients after the roster loop.
