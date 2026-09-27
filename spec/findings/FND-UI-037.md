---
id: FND-UI-037
title: The city-cell, site and sector-cell flashes lighten with white through bitmap 143 and show lit, normal, lit, normal with a one-tick wait after each of the first three copies
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041ACE6..0x0041B4E9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00419AA8..0x0041A0D3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041A0D4..0x0041ACE5
tool: Ghidra 12.1.3
environment: null
---

## Observation

Rectangles are `(left,top)-(right,bottom)`, half-open. Surface 0 is the
window, 1 the back buffer, 2 the city map, 5 the site images, 6 the sheet
`PX00129` and 7 a scratch surface (FND-UI-017). Each function builds its lit
copy on surface 7, calls `fn_00449B20` with a colour of 0x7FFF in each
component and `fn_004266A6` with white (0xFFFF in each component), fill 1 and
shade 0, and then makes four copies to the window with `fn_0042773E`, calling
`fn_00464CD9(1)` after each of the first three.

City cell, `fn_0041ACE6(sector)`, for column `c` and row `r`:

1. copies the 54-by-52 cell `(4 + 53c, 3 + 51r)` of surface 2 to surface 7
   `(0,0)`;
2. selects the pattern (`0x0041ADFA`) and fills white over `(1,1)-(53,51)`
   (`0x0041AE8C`);
3. copies the edge tabs keyed on white onto the copy, after writing the digit
   or letter into the tab on the sheet: in column 0 the row tab to `(-3,14)`,
   in column 7 to `(44,14)`, in row 0 the letter tab to `(15,-3)`, in row 7 to
   `(15,42)`;
4. copies surface 7 `(0,0)-(54,52)` to the window at `(6 + 53c, 45 + 51r)`,
   waits, copies the same screen rectangle from the back buffer, waits, copies
   the lit cell again, waits, and copies from the back buffer once more.

Site, `fn_00419AA8(k)`:

1. copies the 120-by-64 site image of site `k` of the selected sector from
   surface 5 to surface 7 `(0,0)`;
2. selects the pattern (`0x00419B91`) and fills white over `(0,0)-(120,64)`
   (`0x00419C23`);
3. copies the frame `(0,171)-(120,235)` of the sheet keyed on white over it,
   and the progress meter when the sector's owner is the active player;
4. makes the four copies to the window at `(86, 228 + 66k)`, 120 by 64, with
   the back buffer as the normal copy.

Nine-sector display cell, `fn_0041A0D4(sector)`:

1. copies the display `(64,60)-(226,216)` of the back buffer to surface 7
   `(0,0)`;
2. selects the pattern (`0x0041A308`) and fills white over the 52-by-50 area
   at `(1 + 53i, 1 + 51j)`, `i` and `j` the column and row of `sector` among
   the neighbours of the selected sector (`0x0041A39A`);
3. copies the frame `(0,15)-(162,171)` of the sheet keyed on white over the
   whole copy and, while `0x00487848` is set, the edge labels;
4. makes the four copies of the whole 162-by-156 display to the window at
   `(64,60)`.

## Interpretation

Through FND-GFX-006, 0x7FFF selects bitmap 143, so a flash turns every other
pixel of the lit area white, starting with the area's top-left pixel. Those
pixels on the area's outermost row and column come from the pen of the
scratch surface, black, so the lit area has a dotted black edge. The other
pixels keep the image. Labels, frames and the meter are drawn after the
lightening and are not lightened.

The screen shows the lit copy for one wait, the normal image for one wait,
the lit copy for one wait, and then the normal image: three waits of up to
166 ms each (RULE-TIMER-004). FND-UI-017's wording of the city-cell flash
leaves the order open; it is the same as the site and sector-cell flashes of
FND-UI-018.

## Alternatives

- The black edge rests on the default pen of slot 11 (FND-GFX-006).

## How to reproduce

In each of `0x0041ACE6`, `0x00419AA8` and `0x0041A0D4`, find the pushes of
`0x7FFF` before `0x00449B20`, the pushes of `0xFFFF` before `0x004266A6` with
1 and 0, the fill rectangles `(1,1,0x33,0x35)`, `(0,0,0x40,0x78)` and the
point `(1 + 53i, 1 + 51j)` with size `(0x34,0x32)` in `(top,left,bottom,right)`
order, and the four calls to `0x0042773E` with surface 7 or 1 as source and 0
as target, separated by three calls to `0x00464CD9` with 1.
