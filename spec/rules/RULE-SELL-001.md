---
id: RULE-SELL-001
title: Sell removes every selected item but pays half the Cost of only the last selected slot
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EQUIP-002, FND-EQUIP-004, FND-EQUIP-006, FND-EQUIP-007, FND-EQUIP-008, FND-SELL-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001, FMT-DATA-003]
---

## Summary

A gang ordered to Sell gives up each item selected on the Sell panel. The
player receives half of an item's listed Cost, rounded down, but when several
items are sold together only one of them is paid for.

## When it runs

In `transaction_phase`, for a gang whose action is Sell, called by
RULE-EQUIP-002 at the gang's place in the player and roster slot order.

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the selling gang, of FMT-STATE-001.

## Inputs

`item_definitions`, `cash`, `cash_earned`, and the gang's `weapon`, `armor`,
`misc` and item selection (`target`).

## Procedure

```text
let mask = gang.target
# the original starts from a stale scratch value (see Edge cases); every
# order the panel builds sets a bit, so a branch below overwrites it
let value = 0
if (mask & 1) != 0:
    value = item_definitions[gang.weapon].cost / 2
    gang.weapon = -1
if (mask & 2) != 0:
    value = item_definitions[gang.armor].cost / 2
    gang.armor = -1
if (mask & 4) != 0:
    value = item_definitions[gang.misc].cost / 2
    gang.misc = -1
cash[player] = cash[player] + value
cash_earned[player] = cash_earned[player] + value
```

## Outputs

No return value. Empties every selected slot, then adds one value to
`cash[player]` and to `cash_earned[player]`. Makes no random draw.

## Edge cases

- Each selected branch overwrites `value`, so a sale of several items pays only
  for the last selected slot in the order weapon, armor, miscellaneous: the
  miscellaneous item if selected, else the armor, else the weapon
  (BUG-SELL-001).
- The value is half the listed Cost, whatever the gang paid; a Factory
  discount does not lower it. The Sell panel draws the same value, `cost / 2`,
  on each row [FND-SELL-001].
- `/` truncates, so an odd Cost is rounded down.
- The cash is available to Equips by later roster slots of the same player in
  the same pass (RULE-EQUIP-002).
- No branch tests that the slot holds an item. The panel lets only a carried
  item be selected and refuses an empty selection [FND-EQUIP-008, FND-SELL-001]. An order
  with no bit set would pay the stale scratch value: the price the last Equip
  of the pass computed, or 485 when there was none; a selected empty slot
  would pay half of the 16-bit word at `0x004A5EE0` [FND-EQUIP-007].

## What the sources say

SRC-MANUAL-GOG, page 33 (Sell), says Sell sells any or all of a gang's items,
each for half its original price, without Factory discounts, rounded down, and
that every highlighted item is sold. It implies that selling several items pays
for each of them, which the executable does not do.

## Differences between builds

None known.

## Open questions

- Whether the computer players can write a Sell order with an empty mask or
  an empty selected slot is not recorded.
