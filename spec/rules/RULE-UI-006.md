---
id: RULE-UI-006
title: Choosing a sector's gang-status marker
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-031, FND-UI-024, FND-UI-026, FND-DETECT-001, FND-HIRE-001, FND-EXE-004, SRC-MANUAL-GOG, FND-UI-017, FND-HIRE-008, FND-SEARCH-004, EXP-UI-004, EXP-UI-005]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

A sector where the player has a gang shows a small circle: green when no enemy
gang the player can see is there, red when one is, with a question mark when one
of the player's gangs there has no order and a mark when a hire is on its way.
A sector with only a hire on its way shows the incoming mark alone, and only
one such sector keeps it at a time.

## When it runs

When the city map draws a sector's marker. The markers are drawn into the map
surface and stay there until the sector is drawn again, and the detailed
sector screen's nine-sector display copies them from it. The map draws every
sector in number order when a human's planning starts, after `0x004906A4` is
set to -1 (FND-UI-024), and when the Search panel closes (FND-SEARCH-004).
Between those, the Hire dock's redraw `fn_00417CBA` runs after every change of
the player's hire orders and draws the marker of the sector it last kept,
which is -1 when no offer was ordered into a sector, then of each sector an
offer is now ordered into, keeping it (FND-UI-017, FND-HIRE-008); and an
order given on the detailed sector screen draws the marker of the selected
sector (FND-UI-015).

## Parameters

- `sector_number` (`INT32`): the sector drawn.

## Inputs

`active_player`, `gangs`, `hire_orders`, `sectors`.

## Procedure

```text
let incoming = 0
for offer in 0..3:
    if hire_orders[active_player * 3 + offer] == sector_number:
        incoming = 4
let record = sectors[sector_number]
if record.gangs_seen[active_player] != 0:
    let enemy = 0
    for p in 0..6:
        if record.gangs_seen[p] != 0 and p != active_player:
            enemy = 1
    let idle = 0
    for slot in 0..81:
        let gang = gangs[active_player * 81 + slot]
        if gang.action == ACTION_NONE and gang.sector == sector_number:
            idle = 2
    return enemy + idle + incoming
if incoming != 0:
    return 8
return -1
```

## Outputs

Returns the marker frame, or -1 for no marker. Frame `f` is the 20-by-20 cell at
`(492, 67 + 20*f)` of `PX00129`, copied keyed on exact white: 0 to 7 combine
enemy presence (1), an idle friendly gang (2) and an incoming hire (4), and 8 is
the incoming mark alone.

The original keeps one saved cell for frame 8. Before it draws the marker of a
sector where `gangs_seen[active_player]` is 0, it copies the saved cell back
over the last sector that got frame 8, which removes that mark. When it then
draws frame 8, it first saves the cell underneath and remembers the sector. The
city map draws the sectors in number order, so after a full draw frame 8 stays
only on an incoming sector with no later sector that lacks the player's
presence.

## Edge cases

- The procedure also runs for sector -1, which lies off the map. Its
  `gangs_seen` byte is never set, so it copies the saved cell back over the
  last sector given frame 8, and since an offer without an order holds -1 in
  `hire_orders`, it then draws frame 8 off the map and remembers -1. The dock
  makes this call whenever no offer was ordered into a sector before the
  change.
- Between full draws, frame 8 stays on a sector from the dock's redraw that
  gave it until the next drawing of a sector without the player's gangs, -1
  included. A change of the hire order that moves the hire elsewhere draws the
  old sector again, which copies the saved cell back (EXP-UI-005).

- An enemy gang the player cannot see does not turn the circle red: the test
  reads the `gangs_seen` bytes the map drawer rebuilds.
- A gang whose slot is empty has `sector` 100 and never matches.
- The circle is drawn when the player can see a gang of its own in the sector;
  the idle test reads the player's own gangs whatever their visibility.

## What the sources say

SRC-MANUAL-GOG, page 20 (City View), describes the icons: a circle with a green
middle for the player's gangs, a red middle when they detect enemy gangs, a
question mark for gangs with no commands, and a mark for a gang hired into the
sector. It agrees with the executable.

## Differences between builds

None known.

## Open questions

- EXP-UI-004 and EXP-UI-005 reach frames 2, 6 and 8, the copy back, sector -1
  and a full redraw that removes frame 8. No run has yet drawn a sector where
  the player sees an enemy gang, one whose gangs all have orders, or an
  incoming mark on a sector after which no sector lacks the player's gangs,
  and no run gave an order on the detailed sector screen with the markers
  logged.
