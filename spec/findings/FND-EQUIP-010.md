---
id: FND-EQUIP-010
title: The Equip panel handler's faces, keys and double-clicks, and the chosen row redrawn in the second font of PX00129 inside a green frame
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043DAD9..0x0043EFE4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043EFE5..0x0043F135
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414550..0x0041462E
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges are in FND-EXE-004. The Equip handler `fn_0043DAD9` composes the panel
in surface 7 at `(0,144)` from resource 5004 (`0x0043DB69`), so surface 7
`(x, y)` is panel-local `(x, y - 144)` and screen `(x + 104, y - 20)`.
Rectangles below are written `(left,top)-(right,bottom)`.

Set-up, besides the category frame and list of FND-EQUIP-009:

- The acting gang's 64-by-64 portrait from surface 3 goes to surface 7
  `(26,161)`, screen `(130,141)`.
- For each of `weapon`, `armor` and `misc` that is not -1, the item's
  20-by-20 icon, cell `(120, 20 * icon)` of surface 5 with `icon` the item's
  field at offset `0x1E`, goes to surface 7 `(26,226)`, `(48,226)` and
  `(70,226)`, screen `(130,206)`, `(152,206)` and `(174,206)`
  (`0x0043DC26` to `0x0043DE36`).
- When the gang's `action` is already 5, the handler looks for the list row
  whose entry in `0x004948A8` equals `target` (`0x0043DECA`), draws that row's
  mark with `fn_0043EFE5`, and draws the confirm face
  `(137,293)-(187,316)` enabled with `fn_00418E66` (`0x0043DF64`). It does
  not draw the face otherwise. When the stored item is not in the list, the
  row stays -1 while the face is drawn enabled.

Its loop reads events through `fn_00462579` and handles four types:

- Key down (`0x0043DFC6`): Enter (`0x0D`) or Execute (`0x2B`) with a row
  chosen presses the confirm face with `fn_00418CCC(0, ...)`, stores the row's
  entry of `0x004948A8` in `target` (record offset 8, `0x0043E047`) and ends;
  with no row chosen it plays slot 4 (`0x0043E06E`). Escape (`0x1B`) presses
  the Cancel face `(137,261)-(187,284)` with `fn_00418CCC(1, ...)` and ends
  without an order (`0x0043E076`). No other key is tested.
- Left button down (`0x0043E0EB`): outside `(104,124)-(448,333)` it plays
  slot 4. Inside, in panel-local coordinates:
  - the Cancel face, local `(33,137)-(82,159)`, ends without an order when
    `fn_00418821(1, ...)` reports a release inside;
  - the confirm face, local `(33,169)-(82,191)`, plays slot 4 with no row
    chosen, and otherwise stores the order and ends when `fn_00418821(0, ...)`
    succeeds (`0x0043E2D6`);
  - a category cell (FND-EQUIP-005) clears the chosen row, restores the
    category column and draws the frame, rebuilds the list with `fn_0043F136`,
    copies the list area of surface 7 to screen `(251,142)-(432,286)`, and
    draws the confirm face disabled (`0x0043E45F` to `0x0043E650`);
  - the list area, local `(148,26)-(328,169)`, takes row `(y - 26) / 9`. It
    first copies the list area of surface 7 to screen `(251,149)-(432,293)`,
    which removes an earlier mark. A row whose entry is -1 clears the choice
    and draws the face disabled; any other row becomes the choice, gets its
    mark, and the face is drawn enabled (`0x0043E753` to `0x0043E7DE`).
- Left double-click (`0x0043E875`), in local coordinates: on a list row that
  holds an item, the Item Information panel `fn_0044B699` with a copy of that
  item's record; on the three icons, local `(26,82)-(46,102)`,
  `(48,82)-(68,102)` and `(70,82)-(90,102)`, the same panel for the carried
  weapon, armor or miscellaneous item when the slot is not -1; on the portrait,
  local `(26,17)-(90,81)`, the compact gang panel `fn_00455B6B` with a copy of
  the acting gang's record (`0x0043ECD8`). Each redraws the frame, the row mark
  and the face on return. A double-click elsewhere does nothing; it does not
  run the single-press tests.
- Paint (`0x0043ED67`): restores the screen from surface 1 and the panel from
  surface 7, then redraws the frame, the row mark and the face.

`fn_0043EFE5(row)` draws the row mark. It does nothing for -1. For another
row it selects surface 7 for drawing, draws the row's 30-character text, the
entry at `0x00494900 + 31 * row` that the list builder fills with the item's
name padded with spaces, at `(1,1)` of surface 7 with `fn_00414550`, draws the
outline of `(0,0)-(181,9)` with a one-pixel pen of the colour `(0, 255, 0)`
and the null brush through `fn_00426575` with its fill flag 0, and copies that
181-by-9 strip opaquely to the screen at `(251, 149 + 9 * row)`. The strip
lies in the part of surface 7 above the panel.

`fn_00414550(surface, point, text)` has that one caller. It draws a
NUL-terminated text as `fn_00413FD5` does (FND-UI-019), one 6-by-7 cell per
character copied opaquely from surface 6, advancing 6 pixels, but takes the
cell for character `c` from `(6 * (c - 32), 441)` of `PX00129` where
`fn_00413FD5` takes it from `(6 * (c - 32), 0)`.

## Interpretation

The Equip panel's Cancel and confirm faces are the ones every command panel
uses, at screen `(137,261)` and `(137,293)`, 50 by 23. Enter or Execute
confirms and Escape cancels. The chosen item is shown by redrawing its row in
the second font of `PX00129` inside a green frame; the frame is 181 pixels wide
and covers the row's price, which the list draws at screen x 420. A
double-click on a list row, a carried item's icon or the portrait opens that
item's or that gang's information. When the gang's pending item is no longer
listed, the confirm face opens enabled with no row chosen, and pressing it
plays the refusal sound.

## Alternatives

- The glyphs at y 441 of `PX00129` have not been compared with the image; that
  they are a second colour of the same font is read from the matching cell
  size and character order.
- Whether the colour `(0, 255, 0)` shows as pure green in the 8-bit display
  set depends on the palette `fn_00426575`'s pen gets, which has not been read.

## How to reproduce

In `0x0043DAD9`, find the switch on the event type with the tests of `0x2B`,
`0x0D` and `0x1B`, the rectangles `(0x125,0x89,0x13C,0xBB)` and
`(0x105,0x89,0x11C,0xBB)`, the four category rectangles with left `0x68`, the
list rectangle `(0x1A,0x94,0xA9,0x148)` with the division by 9, the three icon
rectangles with top `0x52`, and the call to `0x00455B6B`. In `0x0043EFE5`, find
the call to `0x00414550` with the point `(1,1)` and the entry
`0x00494900 + 0x1F * row`, the colour `(0, 0xFFFF, 0)` passed to `0x00425E99`,
and the copy to top `0x95 + 9 * row`, left `0xFB`. In `0x00414550`, find the
source top `0x1B9`.
