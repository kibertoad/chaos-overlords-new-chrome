---
id: FND-UI-032
title: The main console tests eight fixed tiles, splits five of them by the press row, and acts only on release inside
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004718EE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00419022
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00416C75
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The main-console dispatcher `fn_004718EE` handles pointer input on the city
  and detailed-sector screens. It tests eight half-open rectangles and passes
  cases 0 to 7 to the pressed-control helper `fn_00419022`: Events
  `(500,126)-(548,174)`, Comlink `(552,126)-(600,174)`, Combat
  `(500,178)-(548,226)`, Finance `(552,178)-(600,226)`, Gangs/Hire
  `(500,230)-(548,278)`, Ranking/Search `(552,230)-(600,278)`, Done
  `(500,282)-(600,330)` and Game Info `(588,41)-(614,75)`.
- The helper copies the matching opaque pressed art from `PX00129`: 48-by-48
  cells at `(0,512)`, `(48,512)`, `(96,512)`, `(144,512)`, `(192,512)` and
  `(240,512)` for the six tiles, `(288,512,100,48)` for Done and
  `(190,386,26,34)` for Game Info. It plays slot 2 once, restores the screen's
  own pixels whenever the pointer leaves the same rectangle, draws the pressed
  art again when it comes back, and succeeds only when the button is released
  inside.
- Five tiles choose between two routes by the row of the original press, not by
  a left and right half. Comlink, Combat, Finance and Gangs/Hire give their top
  33 rows to View, Results, City and Gangs and their bottom 15 rows to Send,
  Detailed, Sector and Hire. Ranking/Search gives its top 25 rows to Ranking and
  its bottom 23 rows to Search. The tests are `y > top + 32` and `y > top + 24`,
  so the boundary row belongs to the upper route. Events, Done and Game Info use
  their whole rectangle.
- The Hire input handler `fn_00416C75` on the city and sector screens divides the
  dock into three 66-pixel cells from x 438. Its reject gate is not the whole
  right footer: slot `n` is released inside `(top=437, left=472 + 66*n,
  bottom=450, right=504 + 66*n)`, a 32-by-13 half-open target. Dragging the
  portrait is a separate path.

## Interpretation

The console has eight controls with exact edges. A split tile keeps the route
its press chose even if the pointer crosses the split while the button is held,
and pressing and then moving off a tile before releasing cancels it.

## Alternatives

None known.

## How to reproduce

In `0x004718EE`, find the eight rectangle constructions with the constants
above, each followed by a call to `0x00419022` with a case number. In
`0x00419022`, find the switch on the case, the source coordinates of the
pressed art, and the call to the wrapper `0x00464290` with slot 2.
