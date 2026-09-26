---
id: FND-UI-013
title: The Item Information handler takes an item record and a flag that says whether it opens over another panel
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044B699..0x0044C475
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0044B699` occupies `0x0044B699..0x0044C475` (3,507 bytes, FND-EXE-004). It
has 29 call sites in eight functions: the gang command handler `fn_00414D8C`,
`fn_004169B3`, the gang information panel `fn_00449E80`, the Attack handler
`fn_0043B290`, the Equip handler `fn_0043DAD9`, the Research handler `fn_004427FA`, the Sell handler
`fn_00443BBD` and the Give handler `fn_00445A4F`. Each call site pushes one
byte, then copies the 166-byte item record (FMT-DATA-003) from
`0x004A5F08 + 0xA6 * item` onto the stack above it. The handlers that already
show a panel push 1 (for example at `0x004463C5` in Give); the sector screen's
handlers push 0 (at `0x0041681A`).

Drawing, as FND-UI-004 records it: it saves the screen, loads resource 5001
(`PX05001`) into surface 7 at `(top=144, left=344, bottom=353, right=688)`,
loads the item's strip (resource `4000 + id`) into surface 7 rows 353 to 400,
and draws frame 0, the name, the type, the three description lines, Cost,
Tech Level and the fourteen modifiers. The type is string-table entry
`25 + type` (item offset `0x7A`), right-aligned so that it ends at backing x
624. It then slides in with `fn_0041953E(1)`, the alternate form that shows
backing x 344 to 664 at screen x 128 to 448 (FND-UI-011).

Events:

- Enter or Execute presses the face `(top=293, left=161, bottom=316, right=211)`
  and closes.
- Left button down and left double-click alike: outside `(128,124)-(448,333)`
  they play slot 4; inside, the face at local `(33,169)-(82,191)` from the
  panel corner `(128,124)` closes when released inside. Nothing else reacts.
- Paint restores the screen and the panel. When the flag is 1 it also copies
  backing `(0,144)-(24,353)` to screen `(104,124)-(128,333)`, the strip of the
  panel underneath that the narrower Item Information panel leaves uncovered.
- With no event and the animation timer expired, the next of the 15 frames.

On exit it calls `fn_004196F5(1, flag)` and restores the saved screen with
`fn_004120CB` only when the flag is 0. It sets the byte at `0x00498100` to 1
and writes no game state.

## Interpretation

Item Information (SCR-UI-006) is a read-only panel that can open from the city
screens or on top of a command panel. When it opens over another panel, closing
it leaves that panel on screen instead of restoring the screen saved before
it, so the panel underneath does not need to redraw itself.

## Alternatives

- What `fn_004196F5` does with its second argument has not been read; the
  reading above rests on the handler's own conditional restore.

## How to reproduce

In `0x0044B699`, find the load of resource `0x1389`, the string-table read of
`type + 0x19`, the close face with left `0xA1`, the paint branch that tests the
byte after the record, and the final `fn_004196F5` call with 1 and that byte.
At any caller, find the push of 0 or 1 before `SUB ESP,0xA8` and the copy of
`0x29` dwords and one word from `0x004A5F08`.
