---
id: FND-COMBAT-007
title: The Combat Results page renderer draws one sector's page, the viewer's results and those of one opponent picked from five portrait buttons
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00453087..0x00453A8C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004948F0..0x004948F7
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00453087` occupies `0x00453087..0x00453A8C` (2,566 bytes, FND-EXE-004). All
seven of its call sites are in the Combat Results handler `fn_00451F80`. It
takes the viewing player and an opponent index (0 to 4, or -1) and returns the
opponent index it drew. It draws into surface 7, which the panel shows at
`screen = (x + 104, y - 20)`, and does not copy to the screen itself.

- It draws the page number, the global at `0x004948F0` plus 1, at `(34,157)`
  and the page count at `0x004948F4` at `(70,157)`, both as two-cell numbers
  with the leading-zero flag set.
- It copies the Previous arrow to `(31,177)-(57,200)` from surface 6: the
  26-by-23 art at `(170,363)` on page 0 and at `(118,363)` otherwise.
  It copies the Next arrow to `(59,177)-(85,200)`: the art at
  `(196,363)` on the last page and at `(144,363)` otherwise.
- It fills `(99,171)-(190,328)` and `(242,171)-(333,328)` with black.
- For the page's sector, the entry of the page list at `0x00494AF0` indexed
  by the page, it marks each of the six players that has a result there: the
  first 16-bit value of the player's row in the table at `0x004A8888`
  (stride `0x96` per sector, `0x18` per player) is not -1.
- It draws the viewer's results at `(103,173)` with `fn_00453A8D(viewer, ...)`.
- It copies the sector's 54-by-52 cell from the city map surface 2 to
  `(31,211)` and frames it in black. When any player's police flag for the
  sector (`0x004A8918 + player + 0x96 * sector`) is set it overlays the
  54-by-9 strip at `(0,432)` of surface 6 on the top of the cell. It writes the
  sector's name, a column letter and a row digit, at `(52,266)`.
- It walks the other five players in slot order, storing each in the list at
  `0x00494890`, and draws the player's 32-by-32 portrait (portrait index from
  `0x004A5F00 + player`) at `(202, 160 + 36k)`: from surface 6 row 480 when the
  player has a result in the sector, from row 594 when not. When the opponent
  argument is -1, the first player with a result becomes the opponent.
- When an opponent is chosen, it draws that player's results at `(246,173)`
  with `fn_00453A8D`.

## Interpretation

Each Combat Results page is one sector, and the arrow art taken on the first
and last page is the greyed form. The left list is always the viewer's,
the right list is the opponent's, and the five buttons pick the opponent; a
player with no result in the sector is drawn from the second portrait row,
taken to be the dimmed set. On screen the page counter is at `(138,137)` and
`(174,137)`, the arrows at `(135,157)` and `(163,157)`, the lists at
`(207,153)` and `(350,153)`, the sector cell at `(135,191)`, and the buttons at
`(306, 140 + 36k)`. When no opponent has a result, no right-hand list is drawn
and the function returns -1.

## Alternatives

- Which rows of surface 6 hold the greyed arrows and the dimmed portraits is
  taken from the branch structure; the art has not been compared.
- `fn_00453A8D`, which draws one player's list, has not been read.

## How to reproduce

In `0x00453087`, find the two number calls with `0x004948F0 + 1` and
`0x004948F4`, the arrow copies from surface 6 at y `0x16B`, the loop over six
players testing the word at `0x004A8888`, the two calls to `0x00453A8D` at
`(0x67,0xAD)` and `(0xF6,0xAD)`, and the portrait copies from y `0x1E0` and
`0x252`.
