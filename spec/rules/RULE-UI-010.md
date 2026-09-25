---
id: RULE-UI-010
title: Which gangs the detailed sector cards and Gangs in Sector list
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-036, FND-UI-015, FND-UI-018, FND-UI-002, FND-UI-024, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

The detailed sector screen lists the gangs in the sector of the viewed
player that the active player can see, in roster order. The viewed player is
the active player when the screen opens; clicking another player's lit
portrait in the Overlord bar shows that player's gangs instead. The Gangs in
Sector panel lists the active player's own gangs in the sector.

## When it runs

When the detailed sector screen (SCR-UI-004) or the Gangs in Sector panel
(SCR-UI-005) is drawn. The detailed sector screen is drawn with the active
player as `viewed_player` when it opens, when the selected sector moves, and
after an order; it is drawn with player `p` when the portrait of `p` is
clicked and `sectors[sector].gangs_seen[p]` is set [FND-UI-015].

## Parameters

None.

## Inputs

`active_player`, `viewed_player`, `gangs`.

## Procedure

```text
define sector_card_slots(sector_number) -> INT32[]:
    let slots: INT32[] = []
    for slot in 0..81:
        let gang = gangs[viewed_player * 81 + slot]
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

- A sector holds at most six of a player's gangs (RULE-HIRE-001), so at most
  six cards or columns are drawn. Neither loop has a bound of its own: a
  seventh card would be stored over the selected sector number [FND-UI-018],
  and a seventh Gangs in Sector column would be drawn from x 440, past the
  panel's right edge [FND-UI-024].
- Gangs in Sector is called with the active player and tests only the sector,
  not `visible_to`; it opens only when the player's own `gangs_seen` byte for
  the sector is set.
- The `visible_to` test of the cards is always true for the owner's own gangs.
- Only the active player's own cards take orders; another player's cards open
  the gang and item information panels [FND-UI-015].

## What the sources say

SRC-MANUAL-GOG, page 21 (Sector View), says only the player's gangs in the
sector are displayed, up to six. Page 25 says the upper Gangs button shows all
the player's gangs in the selected sector. They agree with the executable for
the screen as it opens; the manual does not mention the Overlord bar buttons.

## Differences between builds

None known.

## Open questions

None.
