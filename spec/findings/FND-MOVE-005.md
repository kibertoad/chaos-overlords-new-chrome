---
id: FND-MOVE-005
title: The Move panel marks the chosen neighbour with one of eight 32-by-32 keyed arrows from PX00129 placed around the centre cell
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004425AE..0x004427F9
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_004425AE(index)` (range in FND-EXE-004) is called three times, all from
the Move handler `fn_004413EF` (`0x00441C3F`, `0x004422B2`, `0x00442491`),
each time right after the handler has copied the neighbourhood back from
surface 7 to the screen. The handler converts the destination's offset from
the selected sector, -9, -8, -7, -1, +1, +7, +8 and +9, into the index 0 to 7
(FND-MOVE-004).

The function does nothing for -1. For an index `i` from 0 to 7 it copies the
32-by-32 cell `(32 * i, 448)` of surface 6 (`PX00129`, FND-UI-031) with the
keyed mode 1 of `fn_00427864` (FND-PLATFORM-008) to the screen at:

| Index | Offset | Screen corner | Panel-local corner |
|---|---|---|---|
| 0 | -9 | `(273,185)` | `(169,61)` |
| 1 | -8 | `(302,177)` | `(198,53)` |
| 2 | -7 | `(329,185)` | `(225,61)` |
| 3 | -1 | `(265,212)` | `(161,88)` |
| 4 | +1 | `(337,212)` | `(233,88)` |
| 5 | +7 | `(273,238)` | `(169,114)` |
| 6 | +8 | `(302,246)` | `(198,122)` |
| 7 | +9 | `(329,238)` | `(225,114)` |

Any other index leaves the destination uninitialised.

## Interpretation

The chosen destination is marked by an arrow pointing from the gang's sector
toward it, drawn over the neighbourhood around the centre cell
`(290,202)-(344,254)`: the four side arrows overlap the centre cell's edges
and the four diagonal arrows sit near its corners. The eight arrows are the
first eight 32-by-32 cells of the `PX00129` row at y 448, in the order of the
offsets. The Give panel draws the fifth of them, index 4, as its recipient
marker (FND-GIVE-002).

## Alternatives

What the eight cells look like has not been checked against the image; that
each points toward its neighbour is read from their order and placement.

## How to reproduce

In `0x004425AE`, find the switch with the eight destination rectangles
(tops `0xB9`, `0xB1`, `0xB9`, `0xD4`, `0xD4`, `0xEE`, `0xF6`, `0xEE`) and the
source rectangle `(0x1C0, 32 * i, 0x1E0, 32 * i + 32)` passed to `0x00427864`
with surfaces 6 and 0 and mode 1.
