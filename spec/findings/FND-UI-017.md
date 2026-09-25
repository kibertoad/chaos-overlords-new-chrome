---
id: FND-UI-017
title: The city screen is redrawn by a map copy, a selection frame, edge labels, a six-seat Overlord bar, planning lights, a full clock bar and the Hire dock
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041066F..0x0041076F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00411DF5..0x004120A6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004120EF..0x004123CB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00412BF7..0x00413011
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00413012..0x00413857
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00413858..0x00413FD4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00417CBA..0x00418820
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041ACE6..0x0041B4E9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041B4EA..0x0041B667
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041BCD8..0x0041BDD4
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges are those of FND-EXE-004; rectangles are half-open corners
`(left,top)-(right,bottom)` as in FND-UI-015. Surface numbers are the first
two arguments of the opaque copy `fn_0042773E` (source, then destination).
Surface 0 is the window, surface 1 the back buffer that the screens compose
into, surface 2 the prepared city map, surface 6 the sheet `PX00129`
(FND-UI-031) and surface 7 a scratch surface. A point `(x, y)` of surface 2
appears on screen at `(x + 2, y + 42)`.

- `fn_0041066F` redraws the whole city view: it copies surface 2
  `(0,0)-(432,416)` to `(2,42)-(434,458)` of the back buffer, draws the edge
  labels with `fn_00413012(-1)`, copies the same rectangle to the window, and
  redraws the Overlord bar with `fn_00413858(-1, 1)`.
- `fn_00411DF5(sector)` moves the selection. It restores the 54-by-52 cell of
  the old selected sector from surface 2 `(4 + 53c, 3 + 51r)` to
  `(6 + 53c, 45 + 51r)` of the back buffer (column `c`, row `r`) and redraws
  that sector's edge labels. It stores the new sector in `0x004ABC80`, copies
  the 54-by-52 frame at `(236 + 54f, 15)` of the sheet, keyed on white, over
  the new cell, where `f` is the counter `0x00487804` divided by 4 (the pump
  `fn_00462579` counts it from 0 to 7), puts both cells on the window, and
  draws the sector values with `fn_004120EF(sector, 1)`.
- `fn_004120EF(sector, present)` writes, at x 568 of the back buffer: the
  sector's name as a letter `A` to `H` for the column and a digit `1` to `8`
  for the row at y 60; string resource `17 + income` at y 69; `tolerance` at
  y 78; `support` at y 87 and `cash_yield` at y 96, both 0 unless the sector's
  owner is the active player. Numbers are two digits wide. With `present` set
  it copies `(568,60)-(580,103)` to the window.
- `fn_00413012(sector)` draws the grid's edge labels while the byte
  `0x00487848` is nonzero. That byte starts at 1 and no instruction writes it.
  With -1 it draws all of them: the column letter on 23-by-13 tabs copied
  keyed from sheet `(276,448)` to `(21 + 53c, 42)` and from `(276,461)` to
  `(21 + 53c, 444)`, and
  the row number on a 13-by-23 tab from sheet `(299,448)` and `(312,448)` at
  `(3, 59 + 51r)` and `(421, 59 + 51r)`. The glyph is first written into the
  tab on the sheet itself at `(285,449)`, `(285,466)`, `(300,456)` or
  `(319,456)`. With a sector number it redraws only the labels beside that
  sector when it lies on an edge of the grid.
- `fn_00413858(sector, present)` draws the Overlord bar. For each of the six
  seats whose byte in `0x004ABBE0` is nonzero it copies the 32-by-32 portrait
  at `(32 * portrait, 480)` of the sheet, `portrait` being the seat's byte in
  `0x004A5F00`, to `(18 + 70n, 5)`, and fills `(50 + 70n, 5)-(70 + 70n, 25)`
  with black. A seat whose byte is 0 gets the 54-by-32 empty-seat art from
  sheet `(404,448)` instead. With -1 it also sets the viewed player
  `0x00487B8C` to the active player. With a sector number it uses the
  portrait row at y 594 for every seat whose byte `0x10 + n` of the sector
  record is 0, and sets `0x00487B8C` to -1 when no seat has that byte set.
  With `present` set it copies `(18 + 70n, 5)-(78 + 70n, 37)` of each seat to
  the window.
- The pump `fn_00462579` animates the bar on its timer: while
  `0x00487B8C` is not -1 it copies the 20-by-20 frame `(20k, 626)` of the sheet
  to `(50 + 70v, 6)` of the window, `v` being `0x00487B8C` and `k` the counter
  `0x00487B90`, which runs 0 to 11; an empty seat's art cycles through three
  54-by-32 frames at `(404 + 27m, 448)`.
- `fn_0041B4EA` draws, for each seat in play, a 20-by-6 light at
  `(51 + 70n, 30)`: the sheet's `(66,347)-(86,353)` while the seat's byte in
  `0x004ABC58` is nonzero and its byte in `0x004ABC88` is 0, and black
  otherwise. It copies each light to the window when `0x00487B94` is set.
  `0x004ABC58` is set by the setup code for human seats and cleared when a
  network player's seat is handed to the computer; `0x004ABC88` is cleared for
  every seat by the turn function `fn_0046E766` and set by the network session
  code (`fn_0046BA84`, `fn_0046CF38`) and for a seat handed to the computer.
- `fn_0041BCD8` copies the whole 60-by-3 green strip `(354,0)-(414,3)` of the
  sheet to `(520,336)-(580,339)` and to the window. The planning loop calls it
  when planning starts and again when it ends.
- `fn_00417CBA` draws the Hire dock of the active player. For each offer `k`
  it copies the 64-by-64 portrait of the offered gang's definition (chosen by
  the 16-bit `id` at offset `0x1E` of the definition's 156-byte record, the
  records lying at `0x004A2800 + definition * 0x9C`; portraits are laid out ten
  to a row on surface 3) to `(440 + 66k, 373)`.
  When the offer's `hire_orders` element is -2 it adds the keyed 64-by-64
  cross from sheet `(178,299)`; when it holds a sector it adds the keyed stamp
  from sheet `(114,299)` and remembers the sector in `0x004ABC94`. It then
  redraws the gang-status marker of that sector with `fn_00412BF7`, restores
  the previous destination's city cell, or on the sector view redraws the
  nine-sector display (FND-UI-018).
- `fn_00412BF7(player, sector)` draws the gang-status marker of FND-UI-031 at
  surface 2 `(33 + 53c, 19 + 51r)`, that is `(35 + 53c, 61 + 51r)` on screen,
  20 by 20, keyed on white. The state's three bits are: byte `0x10 + p` of the
  sector record set for some player `p` other than `player` (1), one of
  `player`'s gangs in the sector with action 0 (2), and one of `player`'s
  `hire_orders` naming the sector (4). When `player`'s own byte is 0 it only
  keeps the pending-hire frame at `(492,227)` up to date: it restores the
  previous hire destination's marker area from a copy kept at `(120,1320)` of
  surface 5, then saves and draws the new one, remembering it in
  `0x004906A4`.
- `fn_0041ACE6(sector)` flashes a city cell: it makes a lightened copy of the
  54-by-52 cell and its edge labels on surface 7 and puts it on the window,
  waits one tick with `fn_00464CD9(1)`, puts the normal cell back, and does
  both once more. The Hire handler calls it for the sector a portrait is
  dropped on.

## Interpretation

The city map is prepared once on surface 2, with the gang-status markers
written into it, and the screen is refreshed by copying cells back from it.
The Overlord bar sits at `(18 + 70n, 5)`, 70 pixels apart, with the animated
marker of the viewed player 32 pixels to the right of the portrait. The light
under each human seat stays lit until that player's orders are in, which
matters only when several humans play. The planning clock bar sits at
`(520,336)` and starts full.

## Alternatives

- String resources 20 to 24 hold the words the Income row shows; which word
  goes with which Income has not been checked against the string table.
- The meaning of the two selection frames `f = 0` and `f = 1` (a blink or a
  second style) has not been checked in the art.

## How to reproduce

In `0x0041066F`, find the copy between the rectangles `(0,0,416,432)` and
`(42,2,458,434)`. In `0x00411DF5`, find the constants 53, 51, 236, 54 and the
read of `0x00487804`. In `0x004120EF`, find x 568 and the y values 60 to 96. In
`0x00413858`, find the constants 70, 18, 480, 594, 404 and 448. In
`0x0041B4EA`, find 51, 30, 66 and 347. In `0x0041BCD8`, find 520, 336 and
354. In `0x00417CBA`, find 440, 66, 373, 114, 178 and 299. In `0x00412BF7`,
find 33, 19, 492, 227 and 1320.
