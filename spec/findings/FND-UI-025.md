---
id: FND-UI-025
title: The city map surface keeps the unmarked city map in its lower half, from y 416, and the sector view and Detailed Combat take their sector images from there
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00439563..0x004396BF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412411..0x00412445
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410791..0x00410844
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E262..0x0042E336
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges are those of FND-EXE-004. Rectangles are half-open corners
`(left,top)-(right,bottom)`; surfaces are numbered as in FND-UI-017 (2 the city
map surface, 7 the scratch surface).

- `fn_00439563`, called once from the planning entry `fn_0046E766`
  (`0x0046EB2E`), loads image `10000 + city_map_image` (the word at
  `0x00494830`, FND-SAVE-003) into surface 2 at `(0,0)-(432,416)`
  (`0x00439628`), then copies that area of surface 2 to `(0,416)-(432,832)` of
  the same surface (`0x004396A6`).
- The city compositor `fn_004123CC` starts by copying `(0,416)-(432,832)` of
  surface 2 back to `(0,0)-(432,416)` (`0x00412445`), and only then draws the
  owned interiors, the pylons, the site markers and the gang-status markers,
  all at y below 416.
- Every call that writes surface 2 through `fn_0042773E`, `fn_00427864` or the
  image loader `fn_00464108` was listed. Apart from the two copies above, all
  come from `fn_004123CC`, `fn_00412AC4` and `fn_00412BF7`, whose destinations
  lie in the upper half (the lowest, a site marker row, ends at y 408).
- The sector view `fn_00410770` stretches the 52-by-50 area at
  `(5 + 53c, 420 + 51r)` of surface 2 (`0x00410791`), which is the interior of
  the sector's cell in the lower copy: 416 plus the cell interior's y
  `4 + 51r` of FND-UI-017.
- Detailed Combat `fn_0042E040` copies, for each listed sector, the 54-by-52
  area at `(4 + 53c, 419 + 51r)` of surface 2 (`0x0042E28B`), the whole cell in
  the lower copy, to `(31,155)-(85,207)` of surface 7 (`0x0042E336`).

## Interpretation

Surface 2 is 432 by 832 pixels. Its lower half keeps the neutral city map as
loaded, with no ownership colours, pylons or markers, and the city compositor
restores the upper half from it before drawing each new state. The sector
view's background and Detailed Combat's sector image are therefore the plain
map cell: they show the terrain of the sector and never its owner's colour or
any marker. FND-UI-018's "a second copy from y 420" is this copy; it starts at
y 416, and 420 is the row of the first cell interior.

## Alternatives

- `city_map_image` is 0 in every match FND-SAVE-003 records, so the image is
  always `PX10000`; no other value was followed.

## How to reproduce

In `0x00439563`, find the call to `0x00464108` with surface 2 and `10000` plus
the word at `0x00494830`, and the copy from surface 2 to surface 2 with the
rectangles `(0,0,416,432)` and `(416,0,832,432)` in `(top, left, bottom,
right)` order. In `0x004123CC`, find the first copy, with the two rectangles
exchanged. In `0x00410770` find the constant `0x1A4`, and in `0x0042E040` the
constant `0x1A3` and the copy to surface 7. List the callers of `0x0042773E`,
`0x00427864` and `0x00464108` whose destination surface is 2.
