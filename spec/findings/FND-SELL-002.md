---
id: FND-SELL-002
title: The Sell panel marks each selected row with a 192-by-54 keyed overlay from PX00129 and restores the panel's own pixels for the others
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00445655..0x00445A4E
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00445655(weapon, armor, misc)` (range in FND-EXE-004) has seven call
sites, all in the Sell handler `fn_00443BBD` (FND-SELL-001). Each argument is
a byte flag for one row. For row `r` (0 weapon, 1 armor, 2 miscellaneous):

- When the flag is nonzero it copies the 192-by-54 cell `(222,363)` of
  surface 6 (`PX00129`, FND-UI-031) with the keyed mode 1 of `fn_00427864`
  (FND-PLATFORM-008) to the screen at `(214, 138 + 64 * r)`.
- When the flag is 0 it copies the same 192-by-54 area from surface 7, where
  the handler composed the panel at `(0,144)`, from `(110, 158 + 64 * r)` to
  the screen at `(214, 138 + 64 * r)`, which removes a mark drawn earlier.

The area is panel-local `(110, 14 + 64 * r)`, one pixel outside the row
rectangles local `(111, 15 + 64 * r)-(301, 67 + 64 * r)` that the handler
tests for input.

## Interpretation

A selected item row is shown by keyed art drawn over the whole row, the
item's picture, name and price included. The handler's item animation copies
its 48-by-48 frames opaquely to the screen afterwards, so any part of the mark
inside a picture is overwritten on the next frame. The function redraws all
three rows on every call, so the marks always match the three flags.

## Alternatives

What the 192-by-54 cell at `(222,363)` of `PX00129` looks like has not been
checked against the image.

## How to reproduce

In `0x00445655`, find the points `(0x6E, 0x9E)` and `(0xD6, 0x8A)` and their
successors 64 pixels lower, the size 192 by 54 passed to `0x00425F4D`, the
source rectangle `(0x16B,0xDE,0x1A1,0x19E)` passed to `0x00427864` with
surfaces 6 and 0 and mode 1, and the copy from surface 7 to 0 through
`0x0042773E` in the other branch.
