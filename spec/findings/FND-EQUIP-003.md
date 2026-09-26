---
id: FND-EQUIP-003
title: The Give panel's item targets are 52-by-52 cells on a 64-pixel pitch, with up to five recipients in the same panel
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00445A4F
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Give panel handler `fn_00445A4F`, for `DATA/PX08/PX05015`, first
translates the pointer position into the shared panel's local coordinates. Its
three item toggles test the half-open local rectangles `(103,15)-(155,67)`,
`(103,79)-(155,131)` and `(103,143)-(155,195)`, each one pixel larger on every
side than the item picture it surrounds. The same handler shows up to five
eligible friendly recipients in roster order at the local rectangles
`(209,16 + 36*n)-(241,48 + 36*n)`, `n` from 0 to 4. Enter and the Execute key
confirm only when at least one eligible item and a recipient are selected.
Escape and the Cancel control close the panel without an order.

## Interpretation

The item targets are fixed 52-by-52 cells, 64 pixels apart vertically, for the
weapon, armor and miscellaneous slots. The recipient is chosen in the same
panel; there is no second picker screen.

## Alternatives

What makes a recipient eligible (same sector, Tech Level of the item, whether
it is already the target of another Give) has not been recorded. The position
of the Cancel control is not given.

## How to reproduce

Start at `fn_00445A4F`. Its pointer branch compares local x with 103 and 155 and
local y with 15, 67, 79, 131, 143 and 195, and its recipient loop steps y by
36 from 16. Its key branch confirms on Enter and Execute and cancels on
Escape.
