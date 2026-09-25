---
id: RULE-UI-010
title: Which gangs the detailed sector cards and Gangs in Sector list
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-036, FND-UI-002, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

The detailed sector screen and the Gangs in Sector panel list the active
player's own gangs in the sector, in roster order. Enemy gangs never appear
there, even when detected.

## When it runs

When the detailed sector screen (SCR-UI-004) or the Gangs in Sector panel
(SCR-UI-005) is drawn.

## Parameters

None.

## Inputs

`active_player`, `gangs`.

## Procedure

```text
define sector_card_slots(sector_number) -> INT32[]:
    let slots: INT32[] = []
    for slot in 0..81:
        let gang = gangs[active_player * 81 + slot]
        if gang.sector == sector_number and gang.visible_to[active_player] != 0:
            append(slots, slot)
    return slots

define sector_roster_slots(sector_number) -> INT32[]:
    let slots: INT32[] = []
    for slot in 0..81:
        let gang = gangs[active_player * 81 + slot]
        if gang.sector == sector_number:
            append(slots, slot)
    return slots
```

## Outputs

Each function returns the roster slots of the gangs to show, in roster slot
order. The detailed sector screen draws card `n` at
`(254 + 76*(n % 2), 80 + 112*(n / 2))`; Gangs in Sector draws column `n` from
x `258 + 32*n`.

## Edge cases

- A sector holds at most six of a player's gangs, so at most six cards or
  columns are drawn.
- The `visible_to` test of the cards is always true for the owner's own gangs.

## What the sources say

SRC-MANUAL-GOG, page 21 (Sector View), says only the player's gangs in the
sector are displayed, up to six. Page 25 says the upper Gangs button shows all
the player's gangs in the selected sector. They agree with the executable.

## Differences between builds

None known.

## Open questions

- Whose roster Gangs in Sector scans; the active player's is assumed.
- What either list does if more than six gangs match, and whether the card
  list stops at six.
