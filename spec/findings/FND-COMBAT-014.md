---
id: FND-COMBAT-014
title: Detailed Combat draws each gang's header as a colour strip, an Overlord portrait and a name, takes the police header, portrait and items from resource 300, and darkens the last strip frames from tick 12
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042EE46..0x0042F778
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042F98B..0x0043066B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430C23..0x00431C53
tool: Ghidra 12.1.3
environment: null
---

## Observation

The panel is composed in surface 7, whose rows 144 to 353 go to the screen at
`(104,124)` (FND-UI-001), so surface 7 `(x, y)` is panel-local `(x, y - 144)`.
Rectangles are `(left,top)-(right,bottom)`, half-open; fills use
`fn_00426575`, which fills exactly the half-open rectangle (FND-GFX-004).
Copies are opaque (`fn_0042773E`). Positions below are panel-local.

`fn_0042EE46` composes the left side, the viewer's gang. For its owner `p`
(the record's roster index divided by 81):

- black over the strip aperture `(150,130)-(214,194)` (`0x0042EEE7`);
- the colour of `p` from the six-byte record at `0x004ABC18 + 6 * p` over
  `(101,13)-(119,45)` (`0x0042EF54`);
- black over `(153,14)-(213,22)` (`0x0042EFE7`);
- the 32-by-32 Overlord portrait from surface 6 `(32 * f, 480)`, `f` being
  the byte at `0x004A5F00 + p`, to `(119,13)`;
- the player's name, the text at `0x004A2589 + 12 * p`, with `fn_00413FD5` at
  `(153,14)`;
- the gang's 64-by-64 portrait to `(150,48)`, and for each of weapon, armor
  and miscellaneous item either one 48-by-48 frame of the item's strip or, for
  -1, black, at `(100, 48 + 49 * k)` (`0x0042F35D`, `0x0042F55D`,
  `0x0042F75D` for the black fills).

`fn_0042F98B` composes the right side. It first fills black over the strip
aperture `(223,130)-(287,194)` (`0x0042FA2C`) and over the whole header
`(222,11)-(338,47)` (`0x0042FABF`). Then:

- For the police (`0x00494584` equal to -2) it loads resource 300 into surface
  7 at `(0,0)-(324,64)` and copies its `(208,0)-(324,36)` to the header
  `(222,11)`, its `(0,0)-(64,64)` to the portrait `(223,48)`, and its
  `(64,0)`, `(112,0)` and `(160,0)` 48-by-48 areas to the item slots
  `(289,48)`, `(289,97)` and `(289,146)`.
- Otherwise it does as the left side does, at the colour strip
  `(224,13)-(242,45)` (`0x0042FB51`), the Overlord portrait `(242,13)`, the
  name `(276,14)`, the gang portrait `(223,48)` and the item slots
  `(289, 48 + 49 * k)`, filling an empty slot black (`0x0042FED0`,
  `0x004300D6`, `0x004302DC`). It does not clear the name field on its own.

Only `fn_0042F98B` tests for the police; the left side is always a gang.

The clip player `fn_00430C23`, on tick 12 of its timeline, pushes surface 7,
passes 0x7FFF to `fn_00449B20` (`0x0043142B`) and calls `fn_004266A6` with
black, fill 1 and shade 0 over surface 7 `(448,0)-(512,128)` (`0x004314CF`),
the last 64-by-64 frame of both strips. It copies the two darkened frames to
the apertures, pops the surface and copies the panel to the screen. The
strips are loaded again for the next clip.

## Interpretation

Each side's header is an 18-by-32 strip in the owner's colour, the owner's
32-by-32 Overlord portrait and the owner's name in the 6-by-7 font of
`fn_00413FD5` (FND-UI-019). On screen the left header's strip is at
`(205,137)`, its portrait at `(223,137)` and its name at `(257,138)`; the
right header's at `(328,137)`, `(346,137)` and `(380,138)`. The police have
no drawn name: their 116-by-36 header, their portrait and the three pictures
in their item slots are areas of `PX00300`, whose width of 324 pixels the
load rectangle gives.

From tick 12 until the clip ends, both apertures show the last frame of their
strip with every other pixel black, bitmap 143 laid from each frame's corner
(FND-GFX-006).

## Alternatives

- What the areas of `PX00300` show has not been checked against the image.
- Which colours the six-byte records at `0x004ABC18` hold is set by
  `fn_00460CCF` (FND-STATE-007) and not repeated here.

## How to reproduce

In `0x0042EE46`, find the fills with the rectangles `(0x112,0x96,0x152,0xD6)`,
`(0x9D,0x65,0xBD,0x77)` and `(0x9E,0x99,0xA6,0xD5)` in
`(top,left,bottom,right)` order, the copy from surface 6 at y `0x1E0`, and the
call to `0x00413FD5` at `(0x99,0x9E)`. In `0x0042F98B`, find the fills
`(0x112,0xDF,0x152,0x11F)`, `(0x9B,0xDE,0xBF,0x152)` and
`(0x9D,0xE0,0xBD,0xF2)`, the test of `0x00494584` against -2, the load of
resource 300 into `(0,0,0x40,0x144)` and the four copies from it, and the call
to `0x00413FD5` at `(0x114,0x9E)`. In `0x00430C23`, find case 12 of the
timeline switch with `0x7FFF`, `0x00449B20`, black and the rectangle
`(0,0x1C0,0x80,0x200)`.
