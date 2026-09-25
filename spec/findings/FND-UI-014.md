---
id: FND-UI-014
title: The Gangs in Sector handler refuses a sector the player has no gang in, and shows each gang's portrait, Tech Level, Upkeep and effective statistics
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044E6ED..0x0044F2FB
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0044E6ED` occupies `0x0044E6ED..0x0044F2FB` (3,045 bytes, FND-EXE-004). Its
only caller is the main-console dispatcher `fn_004718EE` at `0x00471CAB`. It
takes a player slot and a sector.

- It saves the screen with `fn_004120A7`. When the sector record's byte at
  offset `0x10 + player` (`0x004A08F8 + player + 0x24 * sector`) is 0 it plays
  slot 4, restores the screen and returns without drawing a panel.
- Otherwise it loads resource 5009 (`PX05009`) into surface 7 at
  `(top=144, left=0, bottom=353, right=344)` and copies the sector's 54-by-52
  map cell from surface 2, at `(4 + 53 * column, 419 + 51 * row)`, to surface 7
  `(31,155)`, then draws an unfilled black frame over the same rectangle with
  `fn_00426575`.
- It writes the sector's name as a letter for the column (`'A' + column`)
  followed by a digit for the row (`'1' + row`) at `(52,210)`.
- It scans the player's 81 roster records in order. For each one whose
  `sector` equals the sector it copies the definition's 64-by-64 portrait,
  scaled, into the 32-by-32 cell `(144 + 32n, 158)-(176 + 32n, 190)`, and
  draws two-cell numbers at x `154 + 32n`: the definition's `tech_level` at
  y 192, its Upkeep negated at y 201, the record's Combat, Defense, Stealth and
  Detect at y 211, 220, 229 and 238 (`fn_00414187`), and Chaos, Control, Heal,
  Influence, Research, Strength, Blade, Ranged, Fighting and Martial Arts at
  y 248, 257, 266, 275, 284, 294, 303, 312, 321 and 330 (`fn_004142E7`). The
  column index `n` counts matches and is not compared with any limit.
- It slides the panel in with `fn_0041953E(0)`.

Events: Enter or Execute presses the face `(top=293, left=137, bottom=316,
right=187)` and closes; a left press or double-click outside
`(104,124)-(448,333)` plays slot 4, and on the face at panel-local
`(33,169)-(82,191)` closes when released inside; paint restores the screen and
the panel. On exit it slides the panel out, restores the screen and sets the
byte at `0x00498100` to 1. It writes no game state.

## Interpretation

This is the Gangs in Sector panel (SCR-UI-005). Surface 7 maps to the screen at
`(x + 104, y - 20)`, so the sector cell appears at `(135,135)`, the name at
`(156,190)`, the portraits at `(248 + 32n, 138)` and the value columns start at
x `258 + 32n` on rows 172 to 310. The panel shows only the player's own gangs,
in roster order, one 32-pixel column each; seven or more gangs in one sector
would run past the panel's right edge. The byte at sector offset
`0x10 + player` works as "the player has a gang here".

## Alternatives

- Which code writes the per-player bytes at sector offsets `0x10` to `0x15`,
  and whether they mean exactly "has a gang in the sector", has not been read.

## How to reproduce

In `0x0044E6ED`, find the test of `0x004A08F8` with the slot-4 branch, the load
of resource `0x1391`, the copy from surface 2 with `0x35`, `0x33` and `0x1A3`,
the characters `'A'` and `'1'`, the loop over `0x51` records, and the
rectangles with left `0x90 + 32n` and top `0x9E`.
