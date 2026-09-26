---
id: FND-ATTACK-004
title: A double-click in the Attack picker opens Item Information for an equipment icon and the gang information panel for a portrait, of the acting gang or of a listed target
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043C4BC..0x0043CD6D
tool: Ghidra 12.1.3
environment: null
---

## Observation

In `fn_0043B290` (range in FND-EXE-004), event 5, the double-click
(FND-SEARCH-004, FND-UI-024). Rectangles are `(left, top)-(right, bottom)` in
panel-local coordinates; the handler tests the screen panel
`(104,124)-(448,333)` (`0x0043C4CE`) and subtracts 104 and 124 from the
pointer (`0x0043C514`).

- When the acting gang's definition byte is not -1 (`0x0043C52A`):
  - a double-click in `(26,82)-(46,102)`, `(48,82)-(68,102)` or
    `(70,82)-(90,102)` with the weapon, armor or miscellaneous byte not -1
    copies that item's record from `0x004A5F08 + item * 0xA6` and calls the
    Item Information panel `fn_0044B699` (`0x0043C5B3`, `0x0043C6B0`,
    `0x0043C7AD`);
  - a double-click in `(26,17)-(90,81)` copies the acting gang's 32-byte record
    and calls `fn_00455B6B` (`0x0043C885`), the definition information panel of
    FND-GANG-002.
- For each of the six cells `k` whose list entry at `0x00494850 + 4 * k` is
  not -1 (`0x0043C8FF..0x0043C91B`): the portrait is the 64 by 64 rectangle at
  `(136 + 68 * (k % 3), 17 + 90 * (k / 3))` (`0x0043C932..0x0043C969`), and a
  double-click there copies that gang's record (the chosen opponent, roster
  slot from the list) and calls `fn_00455B6B` (`0x0043C9DD`). Three 20 by 20
  icons lie 65 rows below the portrait's top at x offsets 0, 22 and 44
  (`0x0043CA5B`, `0x0043CB87`, `0x0043CCB3`); a double-click on one whose
  item byte (gang offset `0x04`, `0x05` or `0x06`) is not -1 calls
  `fn_0044B699` for that item (`0x0043CB06`, `0x0043CC32`, `0x0043CD5E`).
- After each panel returns, the handler redraws the opponent marker
  (`fn_0043D93C`), the target marker (`fn_0043D073`) and the Confirm face
  `(137,293)-(187,316)` through `fn_00418E66` with the current enabled flag.
- A double-click anywhere else does nothing; this branch plays no sound.

## Interpretation

Double-clicking a portrait in the picker shows that gang's information panel,
and double-clicking an equipment icon shows the item's information, for the
acting gang and for each listed target. The picker's selection is kept and
redrawn afterwards.

## Alternatives

None known.

## How to reproduce

In `fn_0043B290`, find case 5 of the event switch at `0x0043C4BC`, the
rectangles built with `fn_00425EDF` from `(0x52,0x1A,0x66,0x2E)`,
`(0x52,0x30,0x66,0x44)`, `(0x52,0x46,0x66,0x5A)` and `(0x11,0x1A,0x51,0x5A)`,
the cell loop over `0x00494850`, and the calls to `0x0044B699` and
`0x00455B6B`.
