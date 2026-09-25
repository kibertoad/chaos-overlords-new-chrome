---
id: FND-UI-006
title: Two number helpers draw right-aligned digits in a fixed number of cells, red for negatives, and differ in how they draw zero
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414187
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004142E7
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `fn_00414187` and `fn_004142E7` both walk exactly the number of 6-by-7 cells
  they are given, right-align the decimal digits of a nonzero value, and, for a
  negative value, negate it and take the digits from the red row of surface 6
  (the `PX00129` sheet) at source y 8. Neither draws a minus sign or uses an
  extra cell: -2 is a red 2 in the last cell, and -12 fills two cells with a red
  12.
- `fn_00414187` takes a final flag for leading zeros. Every panel call passes 0,
  so a zero value is the ordinary bright green 0 in the last cell.
- `fn_004142E7` has no such flag and a separate branch for 0: it copies a blank
  into every cell but the last and then copies the cell at source
  `(354,8)-(360,15)`.
- Read from `PX00129`, that cell holds the 0 shape at a dim green intensity
  (14 of 31), where the ordinary digits are bright green (26 of 31) and the
  negative row red.
- Callers: Gang and Gang Definition information use `fn_00414187` for Force,
  Upkeep, Tech Level, Combat, Defense, Stealth and Detect, and `fn_004142E7` for
  the ten Command Skills. Hire and Gangs in Sector use `fn_00414187` for their
  first six rows (Tech Level, Upkeep and the four basic statistics) and
  `fn_004142E7` for the other ten. Item Information uses `fn_00414187` for Cost
  and Tech Level and `fn_004142E7` for all fourteen effects. Site Information
  uses `fn_004142E7` for every number.

## Interpretation

The game draws numbers in two styles. Base values show 0 as a bright 0;
modifiers show 0 as a dim 0, so a modifier that changes nothing stands back.
Both show negatives by colour alone, with no minus sign.

## Alternatives

- The order in which the helpers walk the cells, and so what a value with more
  digits than cells shows, has not been recorded.

## How to reproduce

Open `0x00414187` and `0x004142E7`. Both compare the value with 0, negate it,
and select source row 8 for a negative. `0x004142E7` has a branch for 0 that
copies source x 354, y 8. Decode `DATA/PX16/PX00129` with the layout of
FMT-GFX-001 and compare the cell at `(354,8)` with the digit 0 of the font strip
at `(96,0)`.
