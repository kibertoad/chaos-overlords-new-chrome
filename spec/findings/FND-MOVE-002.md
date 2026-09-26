---
id: FND-MOVE-002
title: The Move panel maps a 162-by-156 area into a three-by-three grid of neighboring sectors
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004413EF
tool: Ghidra 12.1.3
environment: null
---

## Observation

The Move panel handler `fn_004413EF`, which draws `DATA/PX08/PX05006`,
accepts a pointer position inside the shared panel and translates it to
panel-local coordinates. It then tests the panel-local rectangle from
`(132,26)` up to but not including `(294,182)`. It subtracts that rectangle's
top-left corner and divides the result into three columns 54 pixels wide and
three rows 52 pixels high. The cell index is `column + 3 * row`. Index 4, the
acting gang's own sector in the center, is excluded by an explicit test, and a
cell that would lie outside the city is disabled before any destination is
written. Cancel and confirm use the shared panel's common controls, and
confirm is refused when no valid destination has been selected.

## Interpretation

The destination area is one 162-by-156 grid of nine 54-by-52 cells. The eight
outer cells map in row-major order to the sector offsets -9, -8, -7, -1, +1,
+7, +8 and +9 around the acting gang's sector. The center cell shows the
gang's own sector and does nothing when clicked.

## Alternatives

The positions of the common Cancel and confirm controls are not given here.
Whether the handler checks how many friendly gangs are already in, or are
moving into, the chosen sector has not been recorded.

## How to reproduce

Start at `fn_004413EF`, the input handler of the `PX05006` panel. Its pointer branch
compares the local position with 132, 26, 294 and 182, divides by 54 and 52,
and compares the resulting index with 4.
