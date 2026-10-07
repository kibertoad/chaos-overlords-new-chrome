---
id: FND-HIRE-010
title: The dragged hire offer is its 64-by-64 portrait shrunk to 40 by 40 under the setup drag frame, copied opaquely centred on the clamped pointer
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004173C2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041743E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00417560
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00417634
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004177FA
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. The drag part of the Hire handler
`fn_00416C75` (FND-HIRE-008) builds its image before the drag loop. Rectangles
are written `(x1,y1)-(x2,y2)`; `fn_00425EDF(top, left, bottom, right)` builds
one and `fn_0042773E(from, to, source, destination)` and
`fn_00427864(from, to, source, destination, mode)` copy between surfaces
(FND-GFX-004).

- `0x004173C2..0x0041743E`: the offer's portrait number `n`, the word at
  `0x004A281E + 156 * d` for its gang definition `d` (the byte of the offer
  slot in the table at `0x004ABBC0`), gives the source `(64 * (n % 10),
  64 * (n / 10))`, 64 by 64. `fn_0042773E(3, 7, that rectangle, (0,0)-(40,40))`
  copies it from surface 3 to `(0,0)-(40,40)` of surface 7.
- `0x00417446..0x004174DC`: `fn_00427864(6, 7, (150,386)-(190,426),
  (0,0)-(40,40), 1)` copies the area of surface 6 that the setup drag helper
  also lays over its portrait (FND-UI-031) onto the image with mode 1, the
  keyed copy (FND-PLATFORM-015).
- `0x004174E4..0x00417560`: `fn_0042773E(0, 7, ...)` keeps the 40-by-40 area
  of the screen at the point the handler takes as its first argument in
  `(40,0)-(80,40)` of surface 7.
- In the drag loop, after timer slot 1 has ticked (`fn_004328BE(1)` at
  `0x00417620`), the pointer's
  x is held to 20 to 620 and its y to 20 to 440 (`0x00417634..0x0041768C`).
  When the held point differs from the last one, `fn_0042773E(7, 0, ...)` puts
  the kept area back on the screen (`0x00417708`), `fn_0042773E(0, 7, ...)`
  keeps the area `(x-20,y-20)-(x+20,y+20)` of the new point (`0x004177A4`),
  and `fn_0042773E(7, 0, (0,0)-(40,40), that area)` copies the image there
  (`0x004177FA`).

The handler draws nothing else on the screen during the drag: no copy marks
the sector under the pointer.

## Interpretation

An offer in flight is the portrait, shrunk from 64 by 64 to 40 by 40 by the
stretch of `fn_0042773E` for rectangles of different sizes (COLORONCOLOR,
FND-GFX-004), with the setup cards' drag frame keyed over it, drawn opaquely with its centre on the pointer, which stops 20
pixels short of each edge of the window and of y 460. The image moves only on
a tick of slot 1, ten times a second.

## Alternatives

- Which pixels of the frame area are the key colour was not read from the
  sheet.

## How to reproduce

In `0x00416C75`, read the pushes before each call of `0x0042773E` and
`0x00427864` from `0x004173C2` to `0x00417565` and from `0x004176BA` to
`0x004177FF`, the `fn_00425EDF` and `fn_00425F4D` arguments before them, and
the clamp compares with `0x14`, `0x26C` and `0x1B8`.
