---
id: FND-RESEARCH-004
title: The Research panel selects a row on a press, opens Item Information on a double-clicked row and the gang definition on a double-clicked portrait, and shares the command-panel controls
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00442855..0x0044288A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004429C6..0x00442A5D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00442A67..0x0044378E
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_004427FA(player, roster_slot)` (range in FND-EXE-004), called only by the
command handler `fn_00414D8C` (`0x0041514A`, `0x00415836`). Rectangles are
screen coordinates written `(x1,y1)-(x2,y2)`, half-open; "local" means screen
minus `(104,124)`.

- It loads resource 5007 into surface 7 at `(top=144, left=0, bottom=353,
  right=344)` (`0x0044288A`), copies the gang's portrait to local `(26,17)`
  from surface 3, builds the list (FND-RESEARCH-003) and slides the panel in
  with `fn_0041953E(0)`, ending at `(104,124)-(448,333)`.
- When the gang's action is already 11, it searches the sixteen list entries
  at `0x004948A8` for the gang's `target` item, highlights that row with
  `fn_0043EFE5` and enables the confirmation face (`0x004429C6..0x00442A5D`).
- Key down: `0x2B` or `0x0D` plays sound slot 4 when no row is selected, and
  otherwise presses the face `(137,293)-(187,316)`, writes the selected row's
  list entry (an item number) into the gang's byte 8 (`0x00442B40`) and closes
  returning 1. `0x1B` presses `(137,261)-(187,284)` and closes returning 0.
- Left button down, `0x00442BE4..`: outside `(104,124)-(448,333)` it plays
  slot 4. Inside: the Cancel face local `(33,137)-(82,159)` and the
  confirmation face local `(33,169)-(82,191)` work as the keys do, through
  `fn_00418821` (confirmation plays slot 4 with no row selected; its write is
  at `0x00442DCF`). The four category cells (FND-EQUIP-009) choose a category,
  clear the selection, rebuild the list and disable the confirmation face. The
  list area local `(148,26)-(328,169)` gives the row `(y_local - 26) / 9`: a
  row whose entry is -1 clears the selection and disables the face, any other
  row is highlighted with `fn_0043EFE5` and selected (`0x0044324C..0x004432D7`).
- Left double-click, `0x0044336E..`: in local `(148,19)-(328,162)` the row is
  `(y_local - 26) / 9` truncated toward zero; for a row whose entry is not -1
  it copies that item's 166-byte record from `0x004A5F08 + item * 0xA6` and
  opens the Item Information handler `fn_0044B699` with it (`0x00443402`). In
  local `(26,17)-(90,81)`, the portrait, it opens the gang definition handler
  `fn_00455B6B` with the gang record (`0x004434DA`). After either it redraws
  the frame, the selection and the face. A double-click selects nothing.
- Paint restores the screen, the panel, the frame, the selection and the face.

## Interpretation

On the screen the list rows lie from y 150 at nine-pixel steps, the category
cells at `(208, 140 + 36 * n)`, and the Cancel and confirmation faces at
`(137,261)` and `(137,293)` as on the other command panels. A double-click up
to seven pixels above the first row still opens the first row's item, because
its rectangle starts at local y 19 and the division truncates toward zero; a
press there selects nothing. The panel's own controls are Cancel
and confirmation, with Enter and Execute for confirmation and Escape for
Cancel.

## Alternatives

None known.

## How to reproduce

In `fn_004427FA`, read the event switch after `fn_00462579`: the rectangles
`(0x1A, 0x94, 0xA9, 0x148)` for a press and `(0x13, 0x94, 0xA2, 0x148)` for a
double-click in `(top, left, bottom, right)` order, the division by 9, and
the calls to `0x0044B699` and `0x00455B6B`.
