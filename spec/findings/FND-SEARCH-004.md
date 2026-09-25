---
id: FND-SEARCH-004
title: Search rows show the controlled-site icon and the site name, a press flips a row between 0 and 1, the filter is not saved, and the city counts a site as controlled when its progress reaches its Resistance in a sector the viewer owns
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448E32..0x00449924
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449925..0x004499A8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004499A9..0x00449B12
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004123CC..0x00412AC3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A24E8..0x004A256B
tool: Ghidra 12.1.3
environment: null
---

## Observation

Coordinates are panel-local, with the panel at (104, 124) on the screen, unless
marked as screen coordinates.

The Search handler `fn_00448E32`, given the player:

- Loads `PX05024` into the panel part of surface 7 and `PX00150`, 220 by 56,
  into x 0..220, y 0..56 of surface 7, then draws all 22 rows.
- Treats a pointer press (event 3) as follows. Outside the panel it plays
  general-effect slot 4 (`0x00449591`); this is the handler's only use of
  slot 4. Inside, it tests Done (`fn_00418821(0)`, closes on a release
  inside), ALL (`fn_00418821(4)`, screen (137, 140)), NONE (`fn_00418821(5)`,
  screen (137, 172)) and then the 22 row rectangles. `fn_00418821` plays slot
  3 when the press starts, for ALL and NONE as for Done. ALL stores 1 and NONE
  stores 0 in the player's 22 bytes, but only on a release inside the button;
  then all rows are drawn again and the panel is copied to the screen.
- For a press inside row `n`, it stores 1 when the byte is 0 and 0 otherwise
  (`0x00449477..0x004494AF`), draws that row again and copies the row's
  rectangle to the screen.
- Treats a double-click (event 5) inside the panel by calling the Site
  Information handler `fn_0044C476` with the row's definition for the row it
  falls in. That branch writes no filter byte, and a double-click outside the
  panel does nothing.
- On closing, calls the city redraw `fn_004123CC` with the player.

The row painter `fn_004499A9` draws row `n` at (102 + 116 * (n / 11),
22 + 15 * (n % 11)): the 20-by-14 cell at (20 * (n % 11), 14 * (n / 11)) of
`PX00150`, copied with the transparent mode 1, then from 24 pixels right and 3
down the first 15 characters of the site definition's name (from `0x004AB668
+ n * 0x3E`), drawn by `fn_00413FD5` when the byte is set and by
`fn_0041B7D6` when it is 0. It draws no other mark.

The save writer's 44 blocks (FND-SAVE-001) do not include `0x004A24E8`.
`fn_0046E766` stores 0 in all 132 bytes on entry (FND-COMLINK-006).

In the city redraw `fn_004123CC`, the sector loop runs from 0 to 63 and the
slot loop from 0 to 2. A site is drawn as controlled when both hold: its
progress byte (sector record `+8 + 2 * slot`, signed) is not below the 16-bit
Resistance of its definition (at `0x004AB67E + definition * 0x3E`), and the
sector's owner byte equals the player passed in (`0x00412916..0x0041292F`).
Otherwise the filter byte at `0x004A24E8 + player * 22 + definition` is tested
at `0x00412990`.

## Interpretation

Each row shows the site type's white and grey controlled-site icon and its
name, bright when the type is selected and in the second glyph style when it
is not. ALL, NONE and Done all sound the accepted-input click when pressed.
The first click of a double-click flips the row as a single click does; the
double-click then opens Site Information without flipping it back, so the
row ends up changed.

The filter is not part of a saved game. It is emptied whenever the match loop
starts; a load keeps whatever the running game held, unless the load enters
the match loop again, which was not traced.

## Alternatives

- Whether the glyph row at y 274 used by `fn_0041B7D6` looks dimmed was not
  checked against the sheet.
- The mapping of Windows messages to events 3 and 5 is in the event pump and
  was not traced; a double-click reaching the handler without a preceding
  event 3 would leave the row unflipped.

## How to reproduce

In `fn_00448E32`, read the panel test before the slot-4 call, the calls to
`fn_00418821` with 0, 4 and 5, the loops that store 1 and 0 into `0x004A24E8`,
the flip, and the event-5 branch that calls `fn_0044C476`. Open
`fn_004499A9` for the cell and name arithmetic. In `fn_004123CC`, read the
condition before the read of `0x004A24E8` at `0x00412990`.
