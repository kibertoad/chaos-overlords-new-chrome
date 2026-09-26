---
id: FND-MOVE-004
title: The Move panel handler shows the city around the gang, blacks out cells beyond the edge, and stores the chosen sector in the target byte
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004413EF..0x004425AD
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_004413EF` occupies `0x004413EF..0x004425AD` (4,400 bytes, FND-EXE-004). It
is called by the gang command handler `fn_00414D8C` at `0x004157F2`, when the
chosen action is 10, and by `fn_0041462F` at `0x0041488D`. It takes a player
slot and a roster slot and returns 1 when it stored a destination.

Set-up:

- It copies the gang's record, saves the screen, and loads resource 5006
  (`PX05006`) into surface 7 at `(top=144, left=0, bottom=353, right=344)`.
- When the record's `definition` is -1 it copies the 64-by-64 area at
  `(414,363)` of surface 6 to surface 7 `(26,161)`; otherwise it copies the
  definition's portrait there.
- It copies a 162-by-156 area of the city map surface 2 to surface 7
  `(132,170)`, screen `(236,150)`. The area's top-left corner is
  `(5 + 53 * column - 55, 4 + 51 * row - 53)` for the gang's `sector`, so the
  gang's own map cell starts at `(55,53)` in the copy, one pixel right of and
  below the corner of the middle grid cell `(54,52)`.
- It draws with `fn_00426575` in black over that area: first once without
  filling, then filled bands over the grid cells that lie outside the city, the
  top 52 rows when the sector is in row 0, the bottom 52 rows from y + 104 in
  row 7, the left 54 columns in column 0 and the right 54 columns from x + 108
  in column 7.
- It slides the panel in. When the gang's `action` is already 10 it takes the
  stored destination from `target` and converts `target - selected_sector` (the
  global at `0x004ABC80`) into a cell index 0 to 7 for the offsets -9, -8, -7,
  -1, +1, +7, +8 and +9, copies the grid back from surface 7 to the screen,
  draws the highlight with `fn_004425AE(index)` and enables the confirm face
  `(top=293, left=137, bottom=316, right=187)`.

Its event loop handles:

- Key down: Enter or Execute presses confirm and writes the order when a
  destination is set, and plays slot 4 when none is. Escape presses Cancel and
  ends without an order.
- Left button down and left double-click alike: outside
  `(104,124)-(448,333)` slot 4. Inside, in panel-local coordinates, the Cancel
  face local `(33,137)-(82,159)` and the confirm face local
  `(33,169)-(82,191)` work as on the other command panels. The grid local
  `(132,26)-(294,182)` gives column `(x > 53) + (x > 107)` and row
  `(y > 51) + (y > 103)` from its corner. A cell other than the centre whose
  entry in the nine-byte table at `0x004ABC40` is nonzero sets the destination
  to `selected_sector + offset`, redraws the grid from surface 7, draws the
  highlight and enables confirm.
- Paint restores the screen and the panel, redraws the highlight and the face.

Writing the order sets `target` (record offset 8) to the destination sector.
The handler writes no other game state. On exit it slides the panel out,
restores the screen and sets the byte at `0x00498100` to 1.

## Interpretation

The Move panel draws a 3-by-3 crop of the city map centred on the gang and
lets the player pick one of the eight neighbours; cells beyond the city's edge
are black and the table at `0x004ABC40` disables them. The destination is
computed from the selected sector, which is the gang's own sector whenever the
panel is opened from the detailed sector screen. The command box writes the
action.

## Alternatives

- The nine-byte table at `0x004ABC40` is written elsewhere; which function
  fills it, and whether it also excludes cells for reasons other than the
  city's edge, has not been read here.
- When a caller passes a record whose `definition` is -1 (the call from
  `fn_0041462F`) has not been traced.
- `fn_004425AE`, the highlight, is not described here.

## How to reproduce

In `0x004413EF`, find the load of resource `0x138E`, the copy from surface 2
with the multipliers `0x35` and `0x33` and the offsets `-0x37` and `-0x35`,
the four tests of the sector's row and column with the fills, the test of
`action` against 10, the grid rectangle with left `0x84` and top `0x1A`, the
reads of `0x004ABC40`, and the write to `0x00498DB0`.
