---
id: FND-GFX-007
title: Every exact-white pixel of PX00129 that a copy reads lies in a cell copied with the key, except one pixel of an Overlord portrait
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX00129
    offset: 0x36..0xA1836
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042773E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427864
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418821
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418CCC
tool: Ghidra 12.1.3, and a Python 3.14.7 script reading the file's pixel words
environment: null
---

## Observation

`DATA/PX16/PX00129` is 512 by 646 pixels (FND-GFX-003), stored as 16-bit
words from offset `0x36`, bottom row first. 85,137 of its words are `0x7FFF`,
the maximum RGB555 white. Rectangles below are `(left,top)` with a width and
height on the sheet, which startup loads into surface 6 (FND-UI-031).

The source rectangles of every copy from surface 6 through `fn_0042773E` and
`fn_00427864` were read from the calls, each branch of the held-button helpers
`fn_00418821` and `fn_00418CCC` included, with the range of each index the
call adds (FND-PLATFORM-015):

- The keyed copies (mode 1) read these rectangles: `(0,15,162,156)`,
  `(0,171,120,64)`, `(0,235,120,64)`, `(242,299,120,64)`, `(362,299,120,64)`,
  `(114,299,64,64)`, `(178,299,64,64)`, `(66,299,48,48)`, `(120,171,34,34)`,
  `(150,386,40,40)`, `(222,363,192,54)`, `(414,13,54,54)`, `(468,15,44,52)`,
  `(344,15,54,52)`, the two selection frames `(236 + 54 * f, 15, 54, 52)`,
  `(317,560,20,28)`, the eight cells `(32 * p, 448, 32, 32)`, the nine
  gang-status cells `(492, 67 + 20 * s, 20, 20)` and `(492,227,20,20)`, and the
  tabs `(276,448,23,13)`, `(276,461,23,13)`, `(299,448,13,23)`,
  `(312,448,13,23)`, `(325,448,23,13)` and `(348,448,13,23)`. Together they hold
  84,172 of the white pixels.
- The opaque copies of `fn_0042773E` and the mode-0 copies of `fn_00427864`
  read the font cells of rows 0, 8 and 441, the digit cells at
  `(248,274)` and `(248,282)`, the tracks at `(354,0)` and `(354,3)`, the
  64-by-9 strips at `(162, 125 + 9 * a)`, the 32-by-207 strips at
  `(236 + 32 * k, 67)` and `(460,67)`, the 54-by-32 cells at `(404 + 27 * k,
  448)`, the 32-by-32 Overlord portraits of rows 480 and 594, the 20-by-20
  frames of row 626, the button faces at y 363, 386, 409 and 560, the back
  control faces `(120,205,32,63)` and `(460,211,32,63)`, the faces at
  `(120,268)` and `(120,281)`, and the other constant rectangles of their
  calls. Of all these pixels exactly one is `0x7FFF`: `(150,491)`, inside the
  portrait at `(128,480)`.
- The remaining 964 white pixels, 784 in the area from `(112,160)` to
  `(160,208)` around the cell `(120,171,34,34)` and 180 in the area from
  `(320,448)` to `(384,480)` beside the tabs, lie in no source rectangle of
  either copy function.

## Interpretation

On a display where the key matches (RULE-GFX-003), every white pixel the game
reads from the sheet is left out, except the one white pixel of the fifth
Overlord portrait, which the opaque copies of that portrait draw. Whether a
cell is keyed or copied opaquely changes the drawing only where the cell holds
white, so for this sheet the copy mode decides the pixels only for the keyed
cells above and that portrait. FND-UI-031's interpretation that the font holds
white pixels that must be drawn does not hold for this file: its font rows hold
none.

## Alternatives

- Copies that read surface 6 through some function other than `fn_0042773E`
  and `fn_00427864` were not looked for.

## How to reproduce

Read the 512-by-646 pixel words of `DATA/PX16/PX00129` from offset `0x36`,
bottom row first, and count the words equal to `0x7FFF` inside each source
rectangle that the calls of FND-PLATFORM-015 and FND-UI-031 read from surface
6.
