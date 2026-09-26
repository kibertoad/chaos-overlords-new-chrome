---
id: RULE-UI-006
title: Choosing a sector's gang-status marker
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-031, FND-UI-024, FND-UI-026, FND-DETECT-001, FND-HIRE-001, FND-EXE-004, SRC-MANUAL-GOG]
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

When the city map or the detailed sector screen draws a sector.

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

- The removal of frame 8 follows from the copy order and has not been checked
  against a capture of a map with two or more sectors holding only incoming
  hires.
