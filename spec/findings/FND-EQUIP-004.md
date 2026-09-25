---
id: FND-EQUIP-004
title: The Sell panel's item rows are 190-by-52 targets on a 64-pixel pitch that toggle only a filled slot
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00443BBD
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Sell panel handler `fn_00443BBD`, for `DATA/PX08/Px05013`, translates the
pointer position into the shared panel's local coordinates and tests three
half-open local rectangles: `(111,15)-(301,67)`, `(111,79)-(301,131)` and
`(111,143)-(301,195)`. Each toggles its equipment slot (weapon, armor,
miscellaneous) only when the gang has an item in that slot. Cancel and confirm
use the common panel-local positions, and confirm is refused while no item is
selected.

## Interpretation

Sell's three rows are fixed 190-by-52 targets, 64 pixels apart. They do not
cover the whole width of the drawn row with its name and price.

## Alternatives

The positions of the common Cancel and confirm controls, and which keys the
handler accepts, are not given.

## How to reproduce

Start at `fn_00443BBD`, next to the Give handler. Its pointer branch compares
local x with 111 and 301 and local y with 15, 67, 79, 131, 143 and 195, and
tests the gang's item byte before toggling.
