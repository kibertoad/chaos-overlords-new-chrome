---
id: RULE-UI-002
title: Routing a press on the main console
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-032, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-UI-001]
---

## Summary

The main console has eight controls. Five are split into an upper and a lower
button by the row where the pointer was pressed. A control acts when the button
is released over it.

## When it runs

When the left mouse button goes down on the city screen or the detailed sector
screen.

## Parameters

- `press_x` (`INT32`), `press_y` (`INT32`): the pointer position when the button
  went down.

## Inputs

None beyond the parameters and those of RULE-UI-001.

## Procedure

```text
let tile_x: INT32[8] = [500, 552, 500, 552, 500, 552, 500, 588]
let tile_y: INT32[8] = [126, 126, 178, 178, 230, 230, 282, 41]
let tile_w: INT32[8] = [48, 48, 48, 48, 48, 48, 100, 26]
let tile_h: INT32[8] = [48, 48, 48, 48, 48, 48, 48, 34]
let split: INT32[8] = [48, 32, 32, 32, 32, 24, 48, 34]
let upper_route: INT32[8] = [0, 1, 3, 5, 7, 9, 11, 12]
let lower_route: INT32[8] = [0, 2, 4, 6, 8, 10, 11, 12]
for tile in 0..8:
    let left = tile_x[tile]
    let top = tile_y[tile]
    if press_x >= left and press_x < left + tile_w[tile] and press_y >= top and press_y < top + tile_h[tile]:
        let route = upper_route[tile]
        if press_y > top + split[tile]:
            route = lower_route[tile]
        let released_inside = call RULE-UI-001(left, top, tile_w[tile], tile_h[tile])
        if released_inside != 0:
            return route
        return -1
return -1
```

## Outputs

Returns the route, or -1 when the press missed every control or was cancelled:

| Route | Control | Opens |
|---|---|---|
| 0 | Events | The Last Turn Events panel |
| 1 | Comlink, upper | The Comlink View panel |
| 2 | Comlink, lower | The Comlink Send panel |
| 3 | Combat, upper | The Combat Results panel |
| 4 | Combat, lower | Detailed Combat |
| 5 | Finance, upper | The City Financial panel |
| 6 | Finance, lower | The Sector Financial panel |
| 7 | Gangs/Hire, upper | Gangs in Sector (SCR-UI-005) |
| 8 | Gangs/Hire, lower | The Hire comparison panel |
| 9 | Ranking/Search, upper | The Player Ranking panel |
| 10 | Ranking/Search, lower | The Search: Sites panel |
| 11 | Done | Ends the player's planning (RULE-OPTIONS-003 first) |
| 12 | Game Info | Game Information (SCR-UI-008) |

## Edge cases

- The split row belongs to the upper button: a press at `top + 32` (or
  `top + 24` for Ranking/Search) routes upward.
- The route is fixed by the press point; moving across the split while holding
  the button does not change it.

## What the sources say

SRC-MANUAL-GOG, pages 22 to 26 (Main Control Panel), lists the seven buttons of
the panel and says that some are split in two, pressed in their top or bottom
half. Page 18 describes the Game Info button. They agree with the executable.

## Differences between builds

None known.

## Open questions

- Whether the routes do anything different on the detailed sector screen, for
  example which sector the Sector Financial panel reports.
