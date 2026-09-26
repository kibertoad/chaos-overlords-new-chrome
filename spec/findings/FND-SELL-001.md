---
id: FND-SELL-001
title: The Sell panel handler shows each carried item at half its cost and stores the chosen items as a three-bit mask in the target byte
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00443BBD..0x00445654
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_00443BBD` occupies `0x00443BBD..0x00445654` (6,766 bytes, FND-EXE-004). Its
only caller is the gang command handler `fn_00414D8C` at `0x0041586C`, which
calls it when the chosen action is 12 and cancels the action when it returns
0. It takes a player slot and a roster slot and returns 1 when it stored an
order.

Set-up:

- It copies the gang's 32-byte record, saves the screen with `fn_004120A7`,
  loads resource 5013 (`Px05013`) into surface 7 at
  `(top=144, left=0, bottom=353, right=344)` and copies the gang's portrait to
  surface 7 `(26,161)`, as the Give handler does (FND-GIVE-001).
- For each of `weapon`, `armor` and `misc`: when the slot is -1 it fills the
  row's text area, surface 7 `(168,169)-(293,201)`, `(168,233)-(293,265)` or
  `(168,297)-(293,329)`, with black. Otherwise it loads the item's strip
  (resource `4000 + id`), copies frame 0 to surface 7 x 113, y 161, 225 or 289,
  draws the item's name at `(168, 176)`, `(168, 240)` or `(168, 304)` with
  `fn_00413FD5`, and draws `cost / 2` (item offset `0x7E`, C integer division)
  as a two-cell number at `(282, 194)`, `(282, 258)` or `(282, 322)` with
  `fn_00414187`.
- It slides the panel in with `fn_0041953E(0)`. When the gang's `action` is
  already 12 it enables the confirm face `(top=293, left=137, bottom=316,
  right=187)` and restores the selection from bits 0, 1 and 2 of `target`;
  otherwise the selection starts empty. It draws the marks with
  `fn_00445655(weapon, armor, misc)`.

Its event loop handles:

- Key down: Enter or Execute presses the confirm face with `fn_00418CCC(0, ...)`
  and writes the order when at least one item is selected, and plays slot 4
  when none is. Escape presses the Cancel face and ends without an order.
- Left button down: outside `(104,124)-(448,333)` it plays slot 4. Inside, in
  panel-local coordinates, the Cancel face local `(33,137)-(82,159)` and the
  confirm face local `(33,169)-(82,191)` work as in the Give panel, confirm
  playing slot 4 while no item is selected. The rows local
  `(111,15)-(301,67)`, `(111,79)-(301,131)` and `(111,143)-(301,195)` toggle a
  slot that holds an item; after a toggle it redraws the marks and enables the
  confirm face only when at least one item is selected.
- Left double-click: on a row with an item, the Item Information panel
  `fn_0044B699`; on the portrait, local `(26,17)-(90,81)`, the Gang Definition
  Information panel `fn_00455B6B`. Both redraw the confirm face and marks on
  return.
- Paint: restores the screen and the panel and redraws the face and marks.
- With no event and the animation timer expired, it steps a 15-frame counter
  and copies the current 48-by-48 frame of each carried item to screen x 217,
  y 141, 205 and 269.

Writing the order sets `target` (record offset 8) to
`weapon + 2 * armor + 4 * misc`. The handler writes nothing else. On exit it
slides the panel out, restores the screen with `fn_004120CB` and sets the byte
at `0x00498100` to 1.

## Interpretation

The Sell panel shows half of each item's cost, rounded down, as the amount the
sale returns, and lets the player sell any subset of the three carried items
in one order. The order is the item mask in `target`; the command box writes
the action.

## Alternatives

- The displayed half price is what the panel draws; whether the Sell command
  pays the same amount is decided by the resolution code, which this finding
  does not cover.
- The marks drawn by `fn_00445655` have not been read.

## How to reproduce

In `0x00443BBD`, find the load of resource `0x1395`, the three reads of the
item word at `0x004A5F86` divided by 2 before the calls to `0x00414187`, the
test of `action` against 12, the three rectangles with left `0x6F` and right
`0x12D`, and the write to `0x00498DB0`.
