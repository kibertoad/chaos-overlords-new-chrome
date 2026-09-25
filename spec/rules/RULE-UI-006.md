---
id: RULE-UI-006
title: Choosing a sector's gang-status marker
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-031, FND-DETECT-001, FND-HIRE-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-001]
---

## Summary

A sector where the player has gangs shows a small circle: green when no enemy
gang the player can see is there, red when one is, with a question mark when one
of the player's gangs has no order and a mark when a hire is on its way. A
sector with only a hire on its way shows the incoming mark alone.

## When it runs

When the city map or the detailed sector screen draws a sector.

## Parameters

- `sector_number` (`INT32`): the sector drawn.

## Inputs

`active_player`, `gangs`, `hire_orders`.

## Procedure

```text
let friendly = 0
let idle = 0
let enemy = 0
for p in 0..6:
    for slot in 0..81:
        let gang = gangs[p * 81 + slot]
        if gang.sector == sector_number and gang.visible_to[active_player] != 0:
            if p == active_player:
                friendly = 1
                if gang.action == ACTION_NONE:
                    idle = 2
            else:
                enemy = 1
let incoming = 0
for offer in 0..3:
    if hire_orders[active_player * 3 + offer] == sector_number:
        incoming = 4
if friendly != 0:
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

## Edge cases

- An enemy gang the player cannot see does not turn the circle red.
- A gang whose slot is empty has `sector` 100 and never matches.

## What the sources say

SRC-MANUAL-GOG, page 20 (City View), describes the icons: a circle with a green
middle for the player's gangs, a red middle when they detect enemy gangs, a
question mark for gangs with no commands, and a mark for a gang hired into the
sector. It agrees with the executable.

## Differences between builds

None known.

## Open questions

- Whether the idle test applies only to the active player's gangs, as written.
- Whether an incoming hire is found from `hire_orders` or from another record
  of pending hires.
