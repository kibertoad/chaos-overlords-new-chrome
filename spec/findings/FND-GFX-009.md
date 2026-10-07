---
id: FND-GFX-009
title: Surface 7 holds PX00150 for the Search rows' keyed copy and PX00140 for the network lobbies' keyed seat overlay, and every copied cell holds exact white
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448E8D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448ED5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004499A9..0x00449B12
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040BA08
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040BA9B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040C4A8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040C4C5..0x0040CBA4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00467ABB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046913D..0x0046981C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046BC06
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX00150
    offset: 0x36..0x6076
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX00140
    offset: 0x36..0x2AF96
tool: Ghidra 12.1.3, and a Python 3.14.7 script reading the files' pixel words
environment: null
---

## Observation

Function extents are those of FND-EXE-004. Rectangles are written `(left,top)`
with a width and height; the callers build them with `fn_00425EDF(top, left,
bottom, right)` and the image loader `fn_00464108(surface, number, r1, r2)`
copies an image into that rectangle of the surface (FND-GFX-005). Surface 6
holds `PX00129` (FND-UI-031). The three copies are the mode-1 calls of the
copy wrapper `fn_00427864` that FND-PLATFORM-015 lists without an image.

The Search panel copy `0x00449A91` in `fn_004499A9`:

- The Search handler `fn_00448E32` loads `PX05024` into `(0,144)`, 344 by 209,
  of surface 7 at `0x00448E8D`, then `PX00150` into `(0,0)`, 220 by 56, at
  `0x00448ED5`, and then calls `fn_00449925`, which calls `fn_004499A9` for
  each of the 22 rows, as FND-SEARCH-004 records. The handler's later calls
  of `fn_00449925` (after ALL, after NONE, after a click on a row) follow the
  same loads, and the handler has no other load into surface 7.
- `fn_004499A9(n, selected)` copies surface 7 to surface 7 with mode 1: source
  `(20 * (n % 11), 14 * (n / 11))`, 20 by 14, destination
  `(102 + 116 * (n / 11), 166 + 15 * (n % 11))`, 20 by 14. It writes the name
  at 24 pixels right and 3 down from the destination, also on surface 7. Every
  destination lies at y 166 or below, inside the panel, so no row overwrites a
  source cell.
- The cells read are the 22 cells of the top 28 rows of `PX00150`.

The seat copies `0x0040CA34` in `fn_0040C4C5` and `0x004696AC` in
`fn_0046913D`:

- The two functions have the same body and differ only in their constants.
  Each draws six 32-by-32 portraits from surface 6 to the screen, then, for
  each of the four seats, either copies the seat's area from surface 1 to the
  screen when the seat at `0x004905A8` is -1, or composes the seat's card in
  the area `(0,300)`, 76 by 68, of surface 7 and copies it to the screen at the
  seat's point with 3 subtracted from y. The seat points are `(239,125)`,
  `(322,125)`, `(239,199)` and `(322,199)` in `fn_0040C4C5`, and `(385,95)`,
  `(468,95)`, `(385,169)` and `(468,169)` in `fn_0046913D`.
- The card is composed in this order: the screen's area from surface 1 is
  copied opaquely into `(0,300)`, 76 by 68; a 9-by-41 bar is filled at
  `(0,303)` and the seat's name drawn at y 361; the portrait `(32 * p, 480)`,
  32 by 30, of surface 6 is copied to `(12,300)` at 64 by 60, a scaled copy
  through `fn_0042773E`; then the mode-1 copy reads surface 7 `(220,138)`, 64
  by 62, and writes it at `(12,300)`, 64 by 62.
- `fn_0040C4C5` is called three times by the Join handler `fn_0040B9C0`
  (FND-AUDIO-006), at `0x0040BCBA`, `0x0040BFBD` and `0x0040C381`, and once by
  the network dispatcher `fn_0046BA84` at `0x0046BC06`, for a packet of type 2
  (FND-NET-001). `fn_0040B9C0` loads `PX00140` into `(0,0)`, 312 by 282, of
  surface 7 at `0x0040BA9B`, unconditionally and before all three calls. The
  dispatcher reaches its call only while the byte `0x00487B64` or `0x00482178`
  is set. `0x00487B64` is written only at `0x0040BA08` (1) and `0x0040C4A8` (0),
  both in `fn_0040B9C0`.
- `fn_0046913D` is called four times by the Host handler `fn_004677F0` and
  once by `fn_0046892E`, which only `fn_004677F0` and the dispatcher call.
  `fn_004677F0` loads `PX00140` into `(0,0)`, 312 by 282, of surface 7 at
  `0x00467ABB`, in the branch that holds every one of its calls of
  `fn_0046913D` and `fn_0046892E`.
- Every write to surface 7 in `fn_0040C4C5` and `fn_0046913D` is at y 300 or
  below, so the copy reads the image loaded into `(0,0)`.

The pixel words of the two files, read from offset `0x36`, bottom row first,
at the sizes the loader is given (FND-GFX-003):

- `PX00150`, 220 by 56, holds 3,953 words equal to `0x7FFF`. 1,993 lie in the
  22 cells the Search rows read, and every one of those cells holds at least
  one; the other 1,960 lie in rows 28 to 55.
- `PX00140`, 312 by 282, holds 12,032 words equal to `0x7FFF`. 3,848 of the
  3,968 pixels of `(220,138)`, 64 by 62, are `0x7FFF`, its two bottom rows
  entirely. The other 120 pixels form two arrows, one near each side edge.
- Neither file has a word with bit 15 set.

## Interpretation

On screen, `fn_004499A9` draws one row of the Search panel (SCR-SEARCH-001,
FND-SEARCH-004), and its keyed copy draws the site type's icon from
`PX00150`. `fn_0040C4C5` draws the seat cards of the Join lobby
(SCR-NET-002) and `fn_0046913D` those of the Host lobby (SCR-NET-001); their
keyed copy lays the arrow overlay of `PX00140`, the same rectangle the local
setup's copy at `0x0040F3F6` reads (FND-PLATFORM-015), over the seat's
enlarged portrait.

The copy mode decides pixels in all three: each icon cell holds white, and
the overlay is white everywhere but its two arrows. With the key matching
(RULE-GFX-003) the icon shows the panel behind its white pixels, and the
overlay leaves the portrait visible with only the arrows drawn on it. The
overlay's two bottom rows, y 360 and 361, lie below the 60 rows of the
portrait and leave what the card already holds there: the background copied
from surface 1 and, on y 361, the first row of the seat's name.

## Alternatives

- The dispatcher's joining branch also runs while `0x00482178` is set, which
  `fn_0040B9C0` sets at `0x0040BB78` and `fn_00460CCF` sets at `0x004621DB`
  (FND-AUDIO-006). Whether a type-2 packet can reach `fn_0040C4C5`, or a host
  packet `fn_0046892E`, after the lobby handler has returned, with surface 7
  holding another image, was not traced.
- The resumed-network handler `fn_00456F80` also loads `PX00140` into surface
  7 (`0x0045724A`) and calls the dispatcher; which of its paths reach the two
  seat functions was not read.
- A double-click on a Search row opens the Site Information handler
  `fn_0044C476`, which loads `PX05002` into surface 7 at `0x0044C505`, at top
  144 as every `PX05` image (FND-GFX-005). Whether it or a function it calls
  writes the top 28 rows of surface 7 before the Search rows are drawn again
  was not traced.

## How to reproduce

In `fn_00448E32`, read the rectangles before the loads at `0x00448E8D` and
`0x00448ED5` and the calls of `fn_00449925` that follow; in `fn_004499A9`,
read the source and destination arithmetic of the call at `0x00449A91`. In
`fn_0040C4C5` and `fn_0046913D`, read the order of the copies in the seat
loop. List the callers of both functions and read the load of resource 140
into surface 7 in `fn_0040B9C0` and `fn_004677F0`, and the references to
`0x00487B64`. Then count the words equal to `0x7FFF` in `DATA/PX16/PX00150`
(220 by 56) inside the 22 cells and in `DATA/PX16/PX00140` (312 by 282) inside
`(220,138)`, 64 by 62.
