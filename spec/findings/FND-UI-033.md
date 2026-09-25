---
id: FND-UI-033
title: The city map draws the same keyed pylon crop over the six Siege headquarters sectors and the four Big Man centre sectors
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004123CC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00439563
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476726
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494818..0x00494830
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The city compositor `fn_004123CC` first copies the neutral map, replaces the
  52-by-50 interior of each owned sector from the six ownership sheets, and then
  tests the scenario for 6 (Siege) and 8 (Big Man).
- For Siege it compares every sector with the six 32-bit values at
  `0x00494818`. The initializer `fn_00439563` writes the headquarters candidates
  9, 12, 30, 33, 51 and 54 there, and the setup helper `fn_00476726` assigns
  those sectors to the six players.
- For Big Man it tests the literal sectors 27, 28, 35 and 36.
- Both branches build the source rectangle from the helper arguments
  `(15,344,67,398)` in `(top, left, bottom, right)` order, which is
  `(344,15,54,52)` of `PX00129`. Its pixels are two gray pylons on exact white.
- The destination covers the whole 54-by-52 city cell at
  `(4 + 53*column, 3 + 51*row)` on the map surface, and the map is later placed
  at screen offset `(2,44)`. Source and destination are the same size, so
  `fn_00427864(6, 2, ..., 1)` takes its unscaled mode-1 path and drops exact
  white.

## Interpretation

The pylons mark the sectors that matter to the scenario: in Siege each player's
headquarters sector, in Big Man the four centre sectors. The marker is a crop of
the sheet, drawn white-keyed over the cell.

## Alternatives

None known.

## How to reproduce

In `0x004123CC`, find the compares of the scenario with 6 and 8, the loop over
the table at `0x00494818`, and the literals 27, 28, 35 and 36. The source
rectangle constants 15, 344, 67 and 398 are pushed to the rectangle helper
`0x00425EDF`.
