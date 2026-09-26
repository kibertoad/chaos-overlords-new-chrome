---
id: FND-SETUP-014
title: The local setup renderer draws the portrait strip and a 76-by-68 card for each human slot, and restores the background for computer and empty slots
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040EE8A..0x0040F63C
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0040EE8A` occupies `0x0040EE8A..0x0040F63C` (1,942 bytes, FND-EXE-004). It
takes no arguments. Nine of its ten call sites are in the local setup handler
`fn_0040E0A0`; the tenth is in the drag helper `fn_0040F72E` at `0x00410004`.

- It calls `fn_00449B20` with the colour `(0x4000, 0x4000, 0x4000)`, which
  sets the byte at `0x00494868` to 1.
- For each slot 0 to 5 it copies the 32-by-32 portrait `(32 * portrait, 480)`
  of surface 6, where `portrait` is the byte at `0x004A5F00 + slot`, to the
  screen at `(360 + 36 * slot, 38)`.
- It gives each slot a card origin: `(385,95)`, `(468,95)`, `(385,169)`,
  `(468,169)`, `(385,243)` and `(468,243)`.
- When the slot's `controller` (`0x004AB638`) is -1 or 1, it copies the 76-by-68
  area at `(originX, originY - 3)` of surface 1 back to the screen, which
  removes any card drawn there.
- Otherwise it builds the card in surface 7 at `(0,300)-(76,368)`: it copies
  the same background area from surface 1, fills the 9-by-41 bar at `(0,303)`
  with the slot's colour (six-byte colour records at `0x004ABC18`), and writes
  the player's name (`player_names`, `0x004A2588 + 12 * slot`) centred on x 45,
  starting at `45 - 3 * length`, on row 361. It scales the portrait source
  `(32 * portrait, 480)`, 32 wide and 30 high, to 64 by 60 at `(12,300)`. For the slot equal
  to `selected_card` (`0x004854C4`) it then copies the 64-by-62 overlay at
  `(220,138)` of surface 7 over the portrait with mode 1. It copies the
  finished card to the screen at `(originX, originY - 3)`.

It reads no input and writes no game state.

## Interpretation

Each human card, local or over the network, fills screen `(originX, originY -
3)` to `(originX + 76, originY + 65)`: the colour bar at its left edge from
`(originX, originY)`, the portrait at `(originX + 12, originY - 3)`, and the
name on screen row `originY + 58`. Computer players and empty slots show only
the screen's background art in that place. The card origins are 12 pixels left
of and one pixel below the hit rectangles of FND-SETUP-005, which start at
`(397,94)`, so the hit rectangle covers the portrait column and leaves out the
colour bar.

## Alternatives

- What `fn_00449B20`'s byte at `0x00494868` selects (it compares the colour's
  first component with 86 and 171) has not been traced to its readers.
- The overlay at `(220,138)` of surface 7 is taken from FND-SETUP-005 to be the
  arrow art of `PX00140`; which call loads it there has not been checked.

## How to reproduce

In `0x0040EE8A`, find the origin constants `0x181`, `0x1D4`, `0x5F`,
`0xA9` and `0xF3`, the test of `0x004AB638` against -1 and 1, the fill with the
colour record at `0x004ABC18`, the name centring on `0x1E` and `0x2D` from the
record at `0x004A2588`, the comparison with `0x004854C4` and the two calls to
`fn_00427864`.
