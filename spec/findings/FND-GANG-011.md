---
id: FND-GANG-011
title: The compact gang panel dims its base values with black through bitmap 143, starting the pattern at each area's corner
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00456646..0x0045693A
tool: Ghidra 12.1.3
environment: null
---

## Observation

In `fn_00455B6B` (FND-GANG-010), once the base values are drawn, the code
pushes surface 7 (`0x00456648`), builds a colour from 0x7FFF in each
component (`0x00456650`) and passes it to `fn_00449B20` (`0x00456699`). It
then builds black from 0, 0, 0 and calls `fn_004266A6` with fill 1 and shade
0 four times (`0x00456740`, `0x004567E7`, `0x0045688E`, `0x00456935`), over
surface 7 `(498,263)-(510,281)`, `(594,263)-(606,281)`, `(498,290)-(510,335)`
and `(594,290)-(606,335)`, and pops the surface.

## Interpretation

Through FND-GFX-006, 0x7FFF becomes 127 and selects bitmap 143, the
alternating `0x55` and `0xAA` rows. Each area, 12 by 18 or 12 by 45, is
covered with black on every other pixel, starting with black at its own
top-left corner, which lies at screen `(282,243)`, `(378,243)`, `(282,270)`
and `(378,270)`. The pixels the pattern keeps show the base-value digits, so
half of each digit's pixels remain.

## Alternatives

- FND-GFX-006 notes that the result at 8-bit depth depends on the palette.

## How to reproduce

In `0x00455B6B`, from `0x00456646`, find the push of 7, the three pushes of
`0x7FFF` before the call to `0x00425E99`, the call to `0x00449B20`, the black
built from three zeros, and the four calls to `0x004266A6` with the
arguments 1 and 0 and the rectangles `(0x107,0x1F2,0x119,0x1FE)`,
`(0x107,0x252,0x119,0x25E)`, `(0x122,0x1F2,0x14F,0x1FE)` and
`(0x122,0x252,0x14F,0x25E)` in `(top,left,bottom,right)` order.
