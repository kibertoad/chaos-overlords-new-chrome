---
id: FND-ATTACK-001
title: The Attack picker's opponent portraits and six target regions are fixed hit rectangles in handler 0x0043B290
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043B290
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D132
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Two functions load the `PX05003` panel image. `0x0043B290` handles the
  pointer and keyboard input of the Attack picker. `0x0043D132`, the other user
  of the resource, has no pointer-handling path.
- `0x0043B290` tests five opponent portraits, each 32 by 32 pixels, with their
  top-left corners at panel-local `(98, 16 + 36 * n)` for `n` from 0 to 4. A
  portrait whose opponent is not enabled does not react.
- It divides the target area, panel-local `(135,16)-(337,193)` half-open, at
  x = 202 and x = 270 and at y = 105. That gives six half-open regions: three
  columns 67, 68 and 67 pixels wide and two rows 89 and 88 pixels high. A
  region whose target is not enabled does not react.
- Choosing another opponent clears the selected target. The confirm path runs
  only when both an opponent and a target are selected. The common Cancel
  control leaves the picker.

## Interpretation

The picker is chosen in two steps, opponent and then target gang. Its hit map
is its own: the portraits sit on a 36-pixel pitch, and the six target regions
cover the whole target area including the space around each card, so a click
anywhere in a card's region selects it.

## Alternatives

None known. The pressed look of the controls and the target marker were not
read.

## How to reproduce

Find the functions that pass the `PX05003` resource number to the image loader.
The one with a pointer path is `0x0043B290`; read its comparisons against 98,
135, 202, 270, 337, 105 and 193 and the 36-pixel stride.
