---
id: RULE-GIVE-001
title: Give empties the giver's selected slots and holds the items for delivery to the recipient after the player's scan
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EQUIP-002, FND-EQUIP-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EQUIP-002, FMT-STATE-001]
---

## Summary

A gang ordered to Give hands one, two or all three of its items to one other
gang of the same player. The giver's slots empty at once; the recipient gets
the items only after all of the player's transactions, replacing what it holds
in those slots.

## When it runs

In `transaction_phase`, for a gang whose action is Give, called by
RULE-EQUIP-002 at the gang's place in the player and roster slot order.

## Parameters

- `gang`: the giving gang, of FMT-STATE-001.
- `pending_weapon`: the player's pending weapon deliveries, one per roster slot.
- `pending_armor`: the pending armor deliveries.
- `pending_misc`: the pending miscellaneous deliveries.

## Inputs

The giver's `weapon`, `armor` and `misc`, its recipient (`target`) and its
item selection (`target_2`).

## Procedure

```text
let recipient = gang.target
let mask = gang.target_2
if (mask & 1) != 0:
    pending_weapon[recipient] = gang.weapon
    gang.weapon = -1
if (mask & 2) != 0:
    pending_armor[recipient] = gang.armor
    gang.armor = -1
if (mask & 4) != 0:
    pending_misc[recipient] = gang.misc
    gang.misc = -1
```

## Outputs

No return value. Empties the giver's selected slots and records each selected
item in the pending list for the recipient's roster slot; RULE-EQUIP-002
delivers them after the player's scan. Changes no cash and makes no random
draw.

## Edge cases

- A later Give to the same recipient slot overwrites the pending item, and the
  earlier giver's item is lost.
- The delivered item replaces what the recipient holds in that slot, including
  an item it bought this turn; the replaced item is lost.
- Two gangs can swap items by giving to each other in the same turn.

## What the sources say

SRC-MANUAL-GOG, pages 30 and 31 (Give), says a gang can give its equipment to
another of the player's gangs: one gang gives to only one other gang but can
give all three items, the recipient's Tech Level must allow the item, a given
item replaces a similar item the recipient has, which is lost, and two gangs
can swap items. The manual does not say when the items arrive.

## Differences between builds

None known.

## Open questions

- Which fields hold the recipient and the item selection, and which bit stands
  for which slot, are not recorded. The procedure uses `target` for the
  recipient's roster slot and bits 1, 2 and 4 of `target_2` in the weapon,
  armor, miscellaneous order the Sell mask uses.
- That an emptied slot is set to -1 is taken from the meaning of -1 in the gang
  record; FND-EQUIP-002 says the giver's item bytes are cleared.
- Whether the resolver checks the recipient's Tech Level, sector or activity is
  not recorded; the panel offers only eligible recipients (FND-EQUIP-003).
