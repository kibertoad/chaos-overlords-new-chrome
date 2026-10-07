---
id: FND-UI-038
title: The nine-sector display labels its centre row and column and each neighbour's on the frame, the sector view darkens its background with black through bitmap 143, and the Overlord bar animates on timer slot 1
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004116C6..0x00411CF5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410856..0x00410927
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462C7A..0x00462EE2
tool: Python 3.14.7 script disassembling the file bytes with Capstone 5.0.7
environment: null
---

## Observation

Ranges are those of FND-EXE-004; rectangles are half-open corners
`(left,top)-(right,bottom)` and surfaces are numbered as in FND-UI-017.
Column `c` and row `r` are those of the selected sector.

Labels of the nine-sector display. After it overlays the frame, and only
while the byte `0x00487848` is set, `fn_00411119` draws six labels into the
162-by-156 display on surface 7 (`0x004116C6..0x00411CF5`). For each it
writes the glyph into a tab on the sheet (surface 6) and then copies the tab
into the display keyed on white with `fn_00427864(6, 7, ...)`:

- Row numbers, on the 13-by-23 tab at sheet `(348,448)-(361,471)`, the glyph
  written by `fn_00414187` at sheet `(349,456)`, that is at `(1,8)` of the
  tab. The number `r + 1` goes to display `(1,66)` always, `r` to `(1,14)`
  when `r > 0` (`0x004118E8`), and `r + 2` to `(1,118)` when `r < 7`
  (`0x004119E8`).
- Column letters, on the 23-by-13 tab at sheet `(325,448)-(348,461)`, the
  glyph written by `fn_00413FD5` at sheet `(334,449)`, `(9,1)` of the tab. The
  letter `0x41 + c` goes to display `(69,1)` always, `0x40 + c` to `(15,1)`
  when `c > 0` (`0x00411AEB`), and `0x42 + c` to `(123,1)` when `c < 7`
  (`0x00411BF0`).

The display is then copied to `(64,60)-(226,216)` of the back buffer
(`0x00411D89`), so on screen the row tabs stand at `(65, 74)`, `(65,126)` and
`(65,178)` and the column tabs at `(79,61)`, `(133,61)` and `(187,61)`.

Darkening of the sector view. After the stretched copy of FND-UI-018 step 3,
`fn_00410770` builds black with `fn_00425E99(out, 0, 0, 0)` (`0x00410863`) and
a grey with `fn_00425E99(out, 0x8000, 0x8000, 0x8000)` (`0x004108A2`), passes
the grey to `fn_00449B20` (`0x004108CF`), and calls `fn_004266A6` with the
rectangle `(2,42)-(434,458)`, the black, 1 and 2 (`0x0041091F`). By
FND-GFX-006, 0x8000 keeps 0x80 = 128, which selects bitmap 143.

Timer of the Overlord bar. The block of the event pump `fn_00462579` that
draws the empty-seat art and the viewed player's marker starts with
`fn_004328BE(1)` (`0x00462C8B`) and runs only when it returns nonzero; it
clears the flag with `fn_004328F8(1)` (`0x00462CA1`). In it:

- For each of the six seats whose byte in `0x004ABBE0` is 0, the 54-by-32
  area `(404 + 27m, 448)-(458 + 27m, 480)` of the sheet goes to
  `(18 + 70n, 5)-(72 + 70n, 37)` of the window, `m` being the 16-bit counter
  `0x0048782C`. The counter then steps and wraps from 2 to 0
  (`0x00462DB4..0x00462DE0`).
- While `0x00487B8C` is not -1, the frame `(20k, 626)-(20k + 20, 646)` of the
  sheet goes to `(50 + 70v, 6)-(70 + 70v, 26)` of the window, and the counter
  `0x00487B90` steps and wraps from 11 to 0 (`0x00462EC0..0x00462EE2`).

## Interpretation

Each label of the display names the row or column of the city it stands
beside; a neighbour off the map gets no label. The sector view's background
is the cell's terrain with every other pixel replaced by black.

FND-TIMER-002 has `WinMain` start timer slot 1 at rate 10, so the empty-seat
art and the marker step once every 100 ms, one frame per tick, and the
marker's twelve frames take 1.2 seconds. The two counters step on the same
ticks; the marker's is set back to 0 whenever a sector view is drawn
(FND-UI-018), the empty seat's never.

## Alternatives

- The glyph colour is the font strip's, which this finding does not read.
- A tick of slot 1 that falls while the pump is not called is not counted
  twice: the flag only records that one or more ticks fell.

## How to reproduce

In `0x00411119`, list the calls to `0x00427864` after the test of
`0x00487848` at `0x004116C8`, with the points passed to `0x00425F8C` and the
rectangles passed to `0x00425EDF` before each. In `0x00410770`, read the three
pushes of 0x8000 before `0x004108A2` and the call to `0x004266A6` at
`0x0041091F`. In `0x00462579`, read the argument 1 of the calls to
`0x004328BE` and `0x004328F8`, the constants 0x194, 0x1CA, 0x1C0 and 0x1E0 of
the empty-seat copy, and the compares of `0x0048782C` with 2 and `0x00487B90`
with 11.
