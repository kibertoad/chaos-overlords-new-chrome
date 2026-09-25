---
id: RULE-GIVE-001
title: Give empties the giver's selected slots and holds the items for delivery to the recipient after the player's scan
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EQUIP-002, FND-EQUIP-003, FND-EQUIP-007, FND-EQUIP-008, FND-GIVE-001, SRC-MANUAL-GOG]
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

- `player`: the player slot that owns the giver.
- `gang`: the giving gang, of FMT-STATE-001.
- `pending_weapon`: the player's pending weapon deliveries, one per roster slot.
- `pending_armor`: the pending armor deliveries.
- `pending_misc`: the pending miscellaneous deliveries.

## Inputs

The giver's `weapon`, `armor` and `misc`, its item selection (`target`) and
its recipient's roster slot (`target_2`), and the recipient's `sector`.

## Procedure

```text
let recipient = gang.target_2
let mask = gang.target
if gangs[player * 81 + recipient].sector == GANG_INACTIVE:
    return
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
- A Give to a gang that died in this turn's combat is not carried out: the
  giver keeps its items [FND-EQUIP-007].
- The resolver checks neither Tech Level nor sector. The panel offers only
  another of the player's gangs in the giver's sector whose definition Tech
  Level is at least the highest Tech Level of the selected items
  [FND-EQUIP-008, FND-GIVE-001]. Selecting a higher-level item in the panel
  drops a recipient already chosen that no longer qualifies [FND-GIVE-001].
- A selected slot that holds no item gives -1, which the delivery skips.

## What the sources say

SRC-MANUAL-GOG, pages 30 and 31 (Give), says a gang can give its equipment to
another of the player's gangs: one gang gives to only one other gang but can
give all three items, the recipient's Tech Level must allow the item, a given
item replaces a similar item the recipient has, which is lost, and two gangs
can swap items. The manual does not say when the items arrive.

## Differences between builds

None known.

## Open questions

None known.
