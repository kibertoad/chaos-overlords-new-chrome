---
id: FND-UI-044
title: A left press on a gang card's portrait holds the individual command handler in its own loops until the button is released, so the planning loop does not run while a gang is held
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047116B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047154C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00415AD1..0x00415B24
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00415B5E..0x00415C6A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00415ECF..0x00415F94
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041611A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00416C75
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418821
tool: Ghidra 12.1.3
environment: null
---

## Observation

Rectangles are written `(x1,y1)-(x2,y2)`, half-open, as `fn_00449B78` tests
them (FND-UI-020). Function extents are those of FND-EXE-004.

- The sector-view handler `fn_00470E24` calls the individual command handler
  `fn_00414D8C(card, point, 1)` at `0x0047116B` for a left or right button
  press in the card area, and `fn_00414D8C(card, point, 2)` at `0x0047154C`
  for a left double-click there, in both cases only for a card holding a gang
  of the active player while `0x00487B8C` is the active player (FND-UI-015).
  `fn_00414D8C` has no other caller.
- `fn_00414D8C` makes the point relative to the card's corner, `(254 + 76 *
  (card mod 2), 80 + 112 * (card / 2))`. After its action-strip menus
  (FND-UI-021), and for either kind, it tests the point against
  `(5,20)-(69,84)` of the card, the 64-by-64 portrait (`0x00415AD1..0x00415B15`),
  and goes on only when the byte at `0x004ABC60` is 0 (`0x00415B1D`). Outside
  the portrait, or with that byte set, it skips to the double-click part at
  `0x00416714`.
- Wait loop (`0x00415B5E..0x00415C5E`). It reads the pointer record through
  `fn_00465B64` and builds `(x-2,y-2)-(x+2,y+2)` around the screen point. Then,
  on each pass, it calls `fn_0045C2CD`, which peeks at one window message and
  dispatches it, reads the record again, marks the press as moved when the
  screen point has left that rectangle, and ends the loop when the left-button
  byte of the record (offset `0xE`, `0x004985A6`) is 0 or the press has moved.
  At `0x00415C6A` an unmoved press goes to `0x00416714`, which does nothing more
  for kind 1.
- Drag loop (`0x00415ECF..0x00415F94`, after the 40-by-40 drag image is built).
  Each pass ends the loop for `0x0041611A` when the left-button byte is 0;
  otherwise it calls `fn_0045C2CD`, reads the pointer, and when timer slot 1
  has ticked (`fn_004328BE(1)`) clamps the point to x 20 to 620 and y 20 to 440
  and moves the drag image.
- At `0x0041611A` the handler reads the release point and gives the order of
  the second input path of FND-TURN-009: a one-off Move to an enabled
  neighbour of the nine-sector display, or a recurring Influence of an
  unfinished site of an owned sector.
- Neither loop calls the event pump `fn_00462579`, and `fn_00414D8C` does not
  call it anywhere. The same holds for the two hold loops of the Hire handler
  `fn_00416C75` (FND-HIRE-008) and the loop of the held-button helper
  `fn_00418821`: each calls `fn_0045C2CD` on every pass, and neither function
  appears among the callers of `fn_00462579`.

## Interpretation

The second input path of FND-TURN-009 is a drag of the gang's portrait on the
detailed sector screen. It starts with a left press on the portrait of one of
the player's own cards, takes effect only once the pointer has left the
rectangle around the press point, two pixels right or down or three pixels left
or up, and ends when the left button comes up. A right
press reaches the same code, but the left-button byte is already clear, so
the wait loop ends on its first pass and no drag starts.

From the press until the left button comes up the game runs inside
`fn_00414D8C`, whether or not the gang is dragged. The planning loop of
`fn_0046FD80` is the caller of the caller, so its expiry test (FND-TIMER-003)
does not run while a gang is held, and a turn whose time runs out during the
hold ends on the loop's next pass after the release, with the order of the
drop already given. Because the loops call only `fn_0045C2CD` and never the
pump, the clock bar of FND-TIMER-003 is not redrawn and its warning sounds do
not play while a gang is held; the presentation ticks that fall in the hold
are lost except the one the timer flag keeps (FND-UI-023). A held offer or a
control held through `fn_00418821` stops the bar in the same way.

## Alternatives

None known.

## How to reproduce

In `0x00470E24`, find the two calls of `0x00414D8C` and the literal 1 and 2
they push. In `0x00414D8C`, find the rectangle pushed at `0x00415AD1` (0x14, 5,
0x54, 0x45), the read of `0x004ABC60` at `0x00415B1D`, the two loops that call
`0x0045C2CD` and test the byte at offset `0xE` of the `fn_00465B64` record, the
clamp constants `0x26C` and `0x1B8`, and the jump to `0x0041611A`. List the
references to `0x00462579` and check that none lies in `0x00414D8C`,
`0x00416C75` or `0x00418821`.
