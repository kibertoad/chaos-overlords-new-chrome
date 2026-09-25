---
id: FND-EQUIP-005
title: The Equip panel has four 32-by-32 category cells and a sixteen-row item list on a 9-pixel pitch
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043DAD9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F136
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043EFE5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004427FA
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The Equip panel handler `fn_0043DAD9`, for `DATA/PX08/PX05004`, translates the
  pointer position into panel-local coordinates. Its four category cells are
  the half-open 32-by-32 local rectangles `(104,16)-(136,48)`,
  `(104,52)-(136,84)`, `(104,88)-(136,120)` and `(104,124)-(136,156)`.
- Choosing a category clears the current choice and rebuilds the list through
  `fn_0043F136`. That helper clears sixteen fixed list entries and then
  examines the 64 item records. In the shipped `DATA/ITEMS`, no category holds
  more than fifteen records.
- The item list's pointer area is the local rectangle `(148,26)-(328,169)`, and
  the handler takes the row as `floor((y - 26) / 9)`. The list drawing helper
  `fn_0043EFE5` uses the same 9-pixel row pitch.
- The Research panel handler `fn_004427FA`, for `DATA/PX08/PX05007`, uses the
  same list entries and computes the row from the same baseline of 26, but
  accepts pointer positions in the local rectangle `(148,19)-(328,162)`. Its
  first row therefore also takes the seven pixels above the drawn list, and its
  last row ends at the same place relative to the list.

## Interpretation

The item list is a fixed list of sixteen rows with no scrolling. The input
cells for the categories are the inset 32-by-32 rectangles above, even where
the category art is larger. Equip and Research draw their lists at the same
pitch but accept clicks in different rectangles.

## Alternatives

Which category each cell selects, and which item types each category lists,
are not recorded here. The keys the Equip handler accepts are not listed.

## How to reproduce

Start at `fn_0043DAD9`. Its pointer branch compares local x with 104 and 136 and
local y with the four category ranges, calls `fn_0043F136` after a category
change, and divides `y - 26` by 9 inside the list rectangle. `fn_004427FA`
repeats the list arithmetic with the top edge at 19.
