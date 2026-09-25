---
id: RULE-EQUIP-004
title: The Equip list offers researched items of the chosen category within the gang's Tech Level that the gang does not already carry
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-EQUIP-005, FND-EQUIP-006, FND-EQUIP-008, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EQUIP-003, FMT-STATE-001, FMT-DATA-002, FMT-DATA-003]
---

## Summary

The Equip panel lists, for the category the player picks, the items the
player has researched that the gang's Tech Level allows and that the gang does
not already carry. Items the player cannot afford are listed too.

## When it runs

When the Equip panel opens and whenever the player picks a category on it
(SCR-EQUIP-001).

## Parameters

- `player`: the player slot giving the order.
- `gang`: the acting gang, of FMT-STATE-001.
- `category`: the category cell picked, 0 to 3.

## Inputs

`item_definitions`, `gang_definitions`, `research_remaining`, and the gang's
`definition`, `weapon`, `armor` and `misc`.

## Procedure

```text
let entries: INT32[] = []
let tech = gang_definitions[gang.definition].tech_level
for item in 0..64:
    let info = item_definitions[item]
    let tab = -1
    if info.type == ITEM_TYPE_MELEE or info.type == ITEM_TYPE_BLADE:
        tab = 0
    else if info.type == ITEM_TYPE_RANGED:
        tab = 1
    else if info.type == ITEM_TYPE_ARMOR:
        tab = 2
    else if info.type == ITEM_TYPE_MISC:
        tab = 3
    if tab != category:
        continue
    if info.tech_level > tech:
        continue
    if research_remaining[item * 6 + player] != 0:
        continue
    if item == gang.weapon or item == gang.armor or item == gang.misc:
        continue
    append(entries, item)
return entries
```

## Outputs

Returns the list of item record numbers, in record order, as `INT32[]`. The
panel draws each with its price from `item_price` (RULE-EQUIP-003). Changes no
state and makes no random draw.

## Edge cases

- Cash is not read: an item the player cannot afford is listed and can be
  ordered, and fails later in RULE-EQUIP-001.
- The Tech Level test allows equal values, and the gang's Tech Level is the
  definition's, which items and sites do not change [FND-EQUIP-008].
- The research test reads the entry of `active_player`, the player at the
  computer, rather than the gang's player; the panel is only opened for the
  active player's own gangs, so the two agree [FND-EQUIP-008].
- The unused entries hold -1 and their rows are blank [FND-EQUIP-008].
- The list has sixteen fixed rows; the shipped item table never fills more than
  fifteen in one category (FND-EQUIP-005). What happens with more than sixteen
  is not known.

## What the sources say

SRC-MANUAL-GOG, page 30 (Equip), says a list of items available to buy appears
after the type of item is picked, and page 39 says a gang cannot equip an item
whose Tech Level is above its own. Page 30 also says most items must be
researched first.

## Differences between builds

None known.

## Open questions

- What the builder does when more than sixteen items qualify is not known: its
  count has no bound and the list arrays hold sixteen entries
  [FND-EQUIP-008].
