---
id: FND-UI-018
title: The detailed sector screen enlarges the sector's map cell as its background and composes the owner strip, a nine-sector display, three sites, the cards and the group strip over it
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410770..0x00411118
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00411119..0x00411D9A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00419AA8..0x0041A0D3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041A0D4..0x0041ACE5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004123CC..0x00412BF6
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges are those of FND-EXE-004; rectangles and surfaces are written as in
FND-UI-015 and FND-UI-017. Column `c` and row `r` are those of the selected
sector.

`fn_00410770(sector, player)`, the sector-view compositor, does the following
in order:

1. Sets the viewed player `0x00487B8C` to `player` and the marker counter
   `0x00487B90` to 0.
2. Draws the sector values with `fn_004120EF(sector, 1)`.
3. Stretches the 52-by-50 area of surface 2 at `(5 + 53c, 420 + 51r)` over the
   whole map area `(2,42)-(434,458)` of the back buffer, then darkens that
   area through one of the one-bit pattern bitmaps of FND-UI-031
   (`fn_00449B20` with a mid grey, `fn_004266A6`).
4. Copies a 32-by-207 strip from sheet `(236 + 32o, 67)` to `(4,43)-(36,250)`,
   where `o` is the sector's owner plus 1 (0 for no owner), and the fixed
   32-by-207 strip at sheet `(460,67)` to `(4,250)-(36,457)`. The lower strip
   holds the back control that FND-UI-015 tests at `(4,394)-(36,457)`.
5. Draws the nine-sector display with `fn_00411119(sector)`.
6. For each of the three site slots `k` it copies the 120-by-64 row
   `64 * site` of surface 5 (the site portraits) to surface 7, overlays the
   frame at sheet `(0,171)-(120,235)` keyed on white, and, when the sector's
   owner is the active player, the progress meter of FND-UI-036 at
   `(10,59)` of the portrait. The resistance it divides by is the 16-bit word
   at `0x004AB67E + site * 0x3E`. The portrait goes to
   `(86, 228 + 66k)-(206, 292 + 66k)`.
7. Clears the six card slots `0x004ABC68` to -1 and, for every one of
   `player`'s 81 gang records whose `sector` byte is `sector` and whose
   `visible_to` byte for the active player is nonzero, composes the card with
   `fn_00410130`, places it at `(254 + 76*(n % 2), 80 + 112*(n / 2))`,
   outlines it one pixel outside in `player`'s colour (three 16-bit values per
   player at `0x004ABC18`), and stores the roster slot in slot `n`. The count
   `n` has no upper bound in this loop.
8. Clears `0x004ABC4C`. When at least two cards were drawn, `player` is the
   active player and `0x004ABC60` is 0, it copies the 152-by-16 strip at
   sheet `(190,425)` to `(253,61)-(405,77)` and sets `0x004ABC4C` to 1.
   `0x004ABC60` is 0 during planning and 1 in the wait loop `fn_00471F06`.
9. Copies the map area to the window and redraws the Overlord bar with
   `fn_00413858(sector, 1)`.

`fn_00411119(sector)` builds the nine-sector display on surface 7: it copies
the 162-by-156 area of surface 2 at `(5 + 53c - 54, 4 + 51r - 52)` there and
sets all nine bytes of `0x004ABC40` to 1. For a sector on an edge of the grid
it fills the row or column of cells that lies off the map with black and
clears their bytes; it also clears the byte of any neighbour whose owner byte
is -2. It overlays the keyed frame at sheet `(0,15)-(162,171)`, draws the row
numbers and column letters of the neighbourhood on the frame's edges while
`0x00487848` is set (FND-UI-017), and copies the result to
`(64,60)-(226,216)` of the back buffer.

`fn_004123CC`, the city map builder, clears the six bytes `0x10` to `0x15` of
every sector record and then, for each player `p` and each gang of `p` whose
`sector` is not 100 and whose `visible_to` byte for the active player is
nonzero, sets byte `0x10 + p` of that gang's sector.

`fn_00419AA8(k)` flashes site portrait `k`: it makes a lightened copy of the
portrait with its frame and meter on surface 7, and alternates it with the
normal portrait at `(86, 228 + 66k)` on the window, waiting one tick with
`fn_00464CD9(1)` between copies, two flashes in all. The individual command
handler calls it at `0x00416451` when the gang is sent to Influence a site
whose progress is not equal to its resistance.

`fn_0041A0D4(sector)` does the same for one of the nine cells of the display:
it lightens the 52-by-50 cell of `sector` inside the display, at cell offset
`(1 + 53i, 1 + 51j)` for its column and row `i`, `j` relative to the
top-left neighbour, and alternates it with the normal display twice. The Hire
handler calls it for a drop on the sector view, and the individual command
handler for a Move destination.

## Interpretation

The detailed sector screen has no background image of its own: it is the
city's cell for the sector, enlarged and darkened. Surface 2 holds a second
copy of the city map from y 420 onward, and the enlargement is taken from
there. Bytes `0x10` to `0x15` of the sector record say which players have a
gang there that the active player can see; the Overlord bar lights those
portraits, the gang-status marker reads them, and the sector view uses them to
choose whose cards to show (FND-UI-015).

A player can hold at most six gangs in a sector, so the missing bound on the
card count is not reached in normal play. A seventh card would be stored at
`0x004ABC80`, the selected sector.

## Alternatives

- What surface 2 holds from y 420 is answered by FND-UI-025: the unmarked
  city map, copied there from y 416 when planning starts.
- The one-tick wait's length in milliseconds depends on timer 0, which this
  finding does not measure.

## How to reproduce

In `0x00410770`, find the stretch copy from `(5 + 53c, 420 + 51r)`, the
constants 236, 460, 67, 207, 86, 228, 66, 171, 190, 425, 253 and 61, and the
store to `0x004ABC4C`. In `0x00411119`, find the stores of 1 and 0 to
`0x004ABC40` to `0x004ABC48` and the final copy to `(64,60)`. In
`0x004123CC`, find the loop that clears `0x004A08F8 + p + sector * 0x24` and
the one that sets it. In `0x00419AA8` and `0x0041A0D4`, find the four copies
to surface 0 separated by calls to `0x00464CD9`.
