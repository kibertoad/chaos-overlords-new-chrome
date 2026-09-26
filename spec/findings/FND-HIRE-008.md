---
id: FND-HIRE-008
title: The console hire handler takes a drop only on a sector the player owns or has a gang in, opens the live-gang panel on a double-click, and 0x004078B8 is the computer players' snub
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00416C75..0x00417CB9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470C90..0x00470CCC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004712B5..0x004712F1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412796..0x00412856
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004078B8
tool: Ghidra 12.1.3
environment: null
---

## Observation

Rectangles are written `(x1,y1)-(x2,y2)`, screen coordinates, half-open.

- The two planning event handlers, `fn_00470A34` (city map) and `fn_00470E24`
  (detailed sector), call `fn_00416C75(point, 1)` for a left button press
  and `fn_00416C75(point, 2)` for a left double-click whenever the pointer is
  in `(440,373)-(636,450)` (`0x00470CAA`, `0x00470DC3`, `0x004712CF`,
  `0x00471660`).
- `fn_00416C75` takes the offer slot from `x - 440`: 0 up to 64, 1 above 64,
  2 above 130 (`0x00416C8C..0x00416CB4`). While the byte at `0x004ABC60` is
  nonzero, only the double-click branch runs (`0x00416CBE`, `0x00416CC6`).
- Double-click with y below 437 (`0x00416E04`, and `0x00416CC6` in the other
  branch): it builds a gang record in a local, with the slot's offered
  definition, sector 100, Force 0, the three equipment bytes `0xFF` and the
  fourteen statistics from the definition as the hire does (FND-HIRE-006),
  passes it through the effective-statistics rebuild `fn_0047781F`
  (`0x00417052`, `0x00416DAE`) and opens the live-gang panel `fn_00449E80`
  (`0x00417095`) with it. The handler never calls `fn_00455B6B`.
- Press or double-click with y at 437 or more, when `x - 440 - 66 * slot` is
  above 32 (`0x00416E23`, `0x004170CA`): it runs the pointer helper
  `fn_00418821(2, ...)` on `(472 + 66 * slot, 437)-(504 + 66 * slot, 450)`.
  When that returns nonzero, the slot's order byte at `0x004A27C8 + player * 3
  + slot` goes from -2 to -1, from -1 to -2, and from any other value to -1,
  and the other two slots' order bytes are set to -1 (`0x00416EA5..0x00416F5E`
  and `0x00417158..0x00417211`). It then calls `fn_00417CBA`.
- Press with y below 437 (`0x004170AB`): it waits, pumping events, until the
  pointer leaves `(x-2,y-2)-(x+2,y+2)` around the press point or the left
  button is released (`0x004172B5..0x00417364`). A release first ends the
  handler with no change. Otherwise it drags a 40-by-40 image of the offer's
  portrait under the pointer, clamping the pointer to x 20..620 and y
  20..440 (`0x0041763B..0x00417687`), until the button is released
  (`0x00417620`).
- On release, a point outside `(2,42)-(434,458)` ends the handler with no
  change (`0x004178E1`). In the city map (byte `0x00487B88` nonzero) the
  sector is `(x - 2) / 54 + ((y - 42) / 52) * 8` (`0x0041790C`). In the
  detailed sector view, a point inside `(64,60)-(226,216)` gives the cell
  `(x - 64) / 54 + ((y - 60) / 52) * 3`; a cell whose byte in the table at
  `0x004ABC40` is 0 ends the handler, and otherwise the sector is the selected
  sector (`0x004ABC80`) plus -9, -8, -7, -1, 0, +1, +7, +8 or +9 for cells 0 to
  8 (`0x00417A35..0x00417AFB`). A point in the map area outside that grid gives
  the selected sector itself (`0x00417C03`).
- The sector is accepted only when its owner byte equals the active player or
  the sector's byte at offset `0x10 + player` is nonzero (`0x00417939`,
  `0x00417B4F`, `0x00417C03`); otherwise the handler ends with no change. An
  accepted drop writes the sector into the slot's order byte and -1 into the
  other two (`0x00417983..0x004179C8` and the two copies after it) and calls
  `fn_00417CBA`. The handler makes no count of gangs in the sector and reads
  no cash.
- The bytes at sector offsets `0x10` to `0x15` have one writer, the city
  renderer `fn_004123CC`. For each player it clears the byte of every sector
  (`0x004127CE`), then for each of the player's 81 records whose sector is not
  100 and whose `visible_to` byte for the active player is nonzero, stores 1 in
  the byte `0x10 + player` of that gang's sector (`0x00412856`).
- `fn_00417CBA`, called after every order change here and at each human
  planning entry (`0x00470247`), redraws each offer's portrait into
  `(440 + 66 * slot, 373)-(504 + 66 * slot, 437)` from surface 3. Over a slot
  whose order is -2 it copies the 64-by-64 image at `(178,299)-(242,363)` of
  surface 6 with the transparent mode 1 (`0x00418034`); over a slot whose order
  is a sector it copies the image at `(114,299)-(178,363)` of surface 6 the same
  way (`0x00418459`) and keeps that sector in the dword at `0x004ABC94`
  (`0x00418542`); a slot with order -1 gets the plain portrait. In the city map
  it then redraws the kept sector's map cell and calls `fn_00413012` on it.
- `fn_004078B8` (33 bytes) stores -2 in the order byte `0x004A27C8 + player *
  3 + slot` for its two arguments and does nothing else. Its ten calls are all
  in the computer players' planner `fn_00458FA0`, each passing the slot that
  selector `0x8E` of `fn_00402D70` returns (FND-AI-011).

## Interpretation

A human player hires by dragging an offer's portrait onto a sector that the
player owns or where one of the player's gangs stands; in the detailed view
the target can be the shown sector or one of its eight neighbours. The drop
does not look at how many gangs the sector already holds: a seventh gang is
refused only when the hire resolves (FND-HIRE-002). Reject toggles a snub and
clears the other slots' orders, confirming FND-HIRE-004 for that branch.
The console shows the current order by drawing one of two 64-by-64 marks from
`PX00129` over the offer's portrait, one for a hire and one for a snub. A
double-click on an offer shows the gang in the live-gang panel as a Force 0
gang with no equipment. `fn_004078B8` belongs to the computer players only.

The bytes `0x10 + p` of a sector record mean "the active player can see a gang
of player `p` in this sector". For the active player's own slot this is "has
a living gang here", since a gang is always visible to its owner.

## Alternatives

What `fn_00413012`, `fn_0041ACE6` and `fn_0041A0D4` draw for the ordered
sector was not read. Surface 6 holds `PX00129` (FND-UI-031). The presence bytes are as fresh as the last city redraw;
no gang moves during planning, so they match the gang records then.

## How to reproduce

In `fn_00470A34` and `fn_00470E24`, find the rectangle `fn_00425EDF(0x175,
0x1B8, 0x1C2, 0x27C)` before each call to `fn_00416C75`. In `fn_00416C75`,
follow the constants `0x1B8`, `0x1B5`, `0x42`, `0x20`, `0x36`, `0x34` and the
reads of `0x004A08F8`. List the references to `0x004A08F8` and the calls to
`fn_004078B8`.
