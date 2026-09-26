---
id: FND-UI-036
title: The detailed sector screen draws truncated site progress and six-pixel Force steps, and lists only the active player's gangs
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410770
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410130
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABC68..0x004ABC80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABC84..0x004ABC85
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The detailed-sector compositor `fn_00410770` computes each site's percentage
  as `progress * 100 / resistance` in integer arithmetic, where `resistance` is
  the site definition's base Resistance, and uses 100 when the Resistance is 0.
  It copies that many pixels from the 100-by-3 green strip at `(354,0)` of
  `PX00129`, whose rows are light green `(148,255,148)`, green `(0,247,0)` and
  dark green `(0,140,0)`. The site frame supplies the matching 100-by-3 red
  track.
- The gang-card compositor `fn_00410130` copies `force * 6` pixels from the same
  green strip over the 60-by-3 red track in the card frame. The red rows are
  `(255,148,148)`, `(247,0,0)` and `(148,0,0)`.
- The card compositor first copies the whole 74-by-110 card from source corners
  `(162,15)-(236,125)`, overlays the 64-by-9 strip of the gang's action from
  `(162, 125 + 9*action)`, places the 64-by-64 gang portrait at card offset
  `(5,20)`, and places up to three 20-by-20 equipment icons at offsets `(5,86)`,
  `(27,86)` and `(49,86)`.
- `fn_00410770` places cards at `x = 254 + 76*(index % 2)` and
  `y = 80 + 112*(index / 2)`, then draws a one-pixel outline in the player's
  colour just outside each finished card.
- Every ordinary caller passes the active player, the global at `0x004ABC84`,
  as the second argument of `fn_00410770`. The compositor clears the six card
  slots at `0x004ABC68`, then scans only that player's 81 gang records at
  `0x00498DA8 + player * 0xA20`. It draws the records whose `sector` byte (offset
  2) is the selected sector and whose `visible_to` byte for the active player
  (offset `12 + player`) is nonzero, in roster slot order.

## Interpretation

Both meters are three-row bevels copied at their exact length: site progress is
truncated, not rounded, and Force moves in six-pixel steps. The cards show at
most six of the active player's own gangs in roster order; gangs of other
players never appear there, even when detected. A red status marker or an
Attack target can therefore point at gangs the cards do not show.

## Alternatives

- The site meter is read as drawn only when the sector's owner is the active
  player; the branch that decides it has not been given an address.
- What a Force above 10, or progress above the Resistance, draws has not been
  read.

## How to reproduce

In `0x00410770`, find the multiply by 100 and the divide by the site's
Resistance, the test for 0, and the constants 254, 76, 80 and 112. In
`0x00410130`, find the multiply of the Force byte by 6 and the source constants
162 and 125.
