---
id: FND-UI-004
title: Item Information uses the 320-pixel alternate panel, a 15-frame rotating item and fixed two-cell numbers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044B699
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414187
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004142E7
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The Item Information handler `fn_0044B699` loads `PX05001` into the alternate
  backing area and closes through the same crop as Game Information: backing
  `(344,144)-(664,353)` to screen `(128,124)-(448,333)`.
- It copies the current 48-by-48 cell of the item's 720-by-48 `PX04xxx` strip to
  backing `(378,161)-(426,209)`, screen `(162,141)-(210,189)`. The frame counter
  starts at 0, advances through 0 to 14, and wraps.
- The item name starts at backing `(444,171)`, and the item type is
  right-aligned to backing x 624. Three description buffers of exactly 30 bytes
  are written at backing x 444, y 189, 198 and 207. Cost and Tech Level are
  two-cell numbers at backing x 516 and x 612, y 236. The fourteen statistic
  fields use the same two x positions on screen rows 243, 252, 270, 279, 288, 297
  and 306. On screen, the name is at `(228,151)`, the type ends at x 408, the
  description rows are at y 169, 178 and 187, and the numbers start at x 300 and
  396.
- The only control is the bottom face at panel-local `(33,169)-(82,191)`. Enter,
  `VK_EXECUTE` and a pointer press on it close the panel; a press elsewhere
  takes the refused-input path.
- Cost and Tech Level go through `fn_00414187`, and the fourteen modifiers
  through `fn_004142E7`, every call with the literal width 2 (FND-UI-006).

## Interpretation

`PX05001` is a 320-by-209 panel drawn through the alternate crop. The item's
three description rows are each exactly 30 characters, drawn as stored, not
reflowed. The item turns on the monitor through its 15 frames.

## Alternatives

- The frame counter advances on some timer tick; which one, and so how fast the
  item turns, has not been recorded.

## How to reproduce

Find the load of resource 5001 in `0x0044B699`, the compare of the frame
counter with 15, and the calls to `0x00414187` and `0x004142E7` with the pushed
width 2.
