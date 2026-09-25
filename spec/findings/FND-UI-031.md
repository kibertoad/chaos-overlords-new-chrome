---
id: FND-UI-031
title: Copies from the PX00129 sheet use opaque, white-keyed and pattern modes depending on the element
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464108
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042773E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427864
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412BF7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004123CC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040F72E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040EE8A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00457B7B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427E60
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449B20
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432954
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Startup loads `PX00129` into surface 6 with `fn_00464108(6, 0x81, ...)`.
  Copies from surface 6 do not share one transparency policy. The code at
  `0x00413012` calls `fn_00427864(6, 1, ..., 1)` repeatedly for elements keyed
  on exact white. `fn_00457B7B` copies the 32-by-32 Overlord portraits from
  source rows 480 to 512 with mode 0 for inactive seats of the `PX00146` setup
  flow, and with the opaque copy `fn_0042773E` for active seats. A scaled copy
  through the wrapper is opaque whatever mode is asked for.
- The opaque copy `fn_0042773E` has 502 direct calls. 501 push a literal source
  surface, 131 of them surface 6. The one other caller is the one-second
  startup benchmark `fn_00432954`, whose only caller passes surface 1 to surface
  0. All 77 direct calls to `fn_00427864` push a literal source surface, 66 of
  them surface 6. Of those 66, found in 26 functions, 64 use mode 1 and 2 use
  mode 0.
- The gang-status compositor `fn_00412BF7` builds a 20-by-20 source at x 492,
  y `67 + state * 20`, and copies it with `fn_00427864(6, 2, ..., 1)` at both of
  its copy sites.
- The setup drag helper `fn_0040F72E` scales the chosen 32-by-32 portrait
  opaquely into a 40-by-40 scratch cell, then copies source corners
  `(150,386)-(190,426)` of surface 6 over it with mode 1, and uses the result as
  the pointer image. The setup renderer `fn_0040EE8A` makes the second mode-0
  request, but its 32-by-30 source is scaled to 64 by 60, so the wrapper takes
  its opaque scaling path. The twelve 20-by-20 active-player frames at source y
  626 are copied with `fn_0042773E`.
- The marker state is three bits, used while the active player has a gang in
  the sector: a detectable gang of another player adds 1, an active gang whose
  `action` is 0 adds 2, and a pending hire into the sector adds 4. The eight
  frames at y 67 to 207 cover all eight states. A pending hire into a sector
  with no friendly gang uses a ninth frame at y 227.
- The presence builder `fn_004123CC` clears a table of 64 sectors by 6 players,
  then sets an entry for a gang only when the gang is active and its
  `visible_to` byte for the current player is nonzero.
- The sheet's pixels: dark green `(0,156,0)` for the uncontested circle, dark
  red `(156,0,0)` for the contested one, yellow `(247,247,0)` for the idle
  question mark, and brighter reds for the incoming-hire decoration.
- The mode compositor `fn_00427E60` picks the one-bit bitmap resource 147, 143
  or 146. Its only selector `fn_00449B20` maps an input below 86 to 147, 86 to
  170 to 143, and 171 or more to 146. Its raster operations come to
  `(destination AND pattern) XOR (source AND NOT pattern)`: a set pattern bit
  keeps the destination and a clear bit takes the source. In the unscaled
  `PX00146` inactive-seat path, the fixed colour `0x2661` selects 146.
- The resources `Chaos Overlords.exe#BITMAP/143`, `#BITMAP/146` and
  `#BITMAP/147` are 8-by-8 one-bit bitmaps with a black and white palette. Read
  as rows from the top, 143 alternates `0x55` and `0xAA` (32 bits set), 146
  alternates `0x88` and `0x22` (16 bits set), and 147 alternates `0xDD` and
  `0x77` (48 bits set).

## Interpretation

`PX00129` cannot be treated as one image with white as transparent everywhere:
its font and portraits contain white pixels that must be drawn. Fonts,
portraits and frames are copied opaquely; the `HIRED` stamp, the snub cross, the
sector back arrow, the gang-status markers, the setup drag frame and the
objective pylons are keyed on exact white. The pattern modes darken an image in
three strengths chosen by a value from 0 to 255.

## Alternatives

- Several rectangles of the sheet copied by the audited calls have not been
  given a role.

## How to reproduce

List the callers of `0x0042773E` and `0x00427864` and the source surface each
pushes. In `0x00412BF7`, find the constants 492, 67 and 20. In `0x00449B20`,
find the compares with 86 and 171. Extract the three bitmap resources and read
their rows.
