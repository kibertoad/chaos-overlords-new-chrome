---
id: FND-INFLUENCE-002
title: The Influence picker draws each site slot as a PX02000 picture with a keyed PX00129 frame, dims completed sites, and shows the chosen slot from a prepared highlighted copy
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044127B..0x004413EE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043F87E..0x00440B23
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418F16..0x00419021
tool: Ghidra 12.1.3
environment: null
---

## Observation

Surfaces: the startup loader `fn_00418F16` loads `PX02000` into surface 5 at
`(0,0)`, 120 pixels wide and 1,408 high, and `PX04999` beside it at x 120,
20 wide and 1,280 high. Surface 6 holds `PX00129` (FND-UI-031). The Influence
handler `fn_0043F692` loads `PX05005` into surface 7 at `(0,144)`, so a
surface 7 point `(x, y)` in that area is panel-local `(x, y - 144)` and screen
`(104 + x, y - 20)`. Mode 0 and mode 1 of the copy wrapper `fn_00427864` are
the pattern mask and the white key of FND-PLATFORM-008.

For each of the sector's three site slots `s`, the handler (`0x0043F87E`
onward) takes the slot's site definition `d` (sector bytes `0x07`, `0x09`,
`0x0B`) and its source picture, the 120-by-64 cell at `(0, 64 * d)` of surface
5. The slot's place in surface 7 is `(106,161)`, `(208,217)` and `(106,271)`
for slots 0, 1 and 2, panel-local `(106,17)`, `(208,73)` and `(106,127)`,
the rectangles FND-INFLUENCE-001 reads for input.

- When the slot's progress byte equals the definition's Resistance (the 16-bit
  field at `0x004AB67E + 62 * d`), the handler fills the slot's place with
  black, copies the picture over it with mode 0, and copies the 120-by-64 cell
  `(362,299)` of surface 6 over that with mode 1. It marks the slot as not
  selectable.
- Otherwise it copies the picture opaquely, then the 120-by-64 cell `(242,299)`
  of surface 6 with mode 1. It also builds a highlighted copy at surface 7
  `(120 * s, 0)`: the picture, opaque, with the 120-by-64 cell `(0,235)` of
  surface 6 over it with mode 1.

`fn_0044127B(slot)` (range in FND-EXE-004; four call sites, all in
`fn_0043F692`) does nothing for -1. For slot 0, 1 or 2 it copies the
highlighted copy at surface 7 `(120 * slot, 0, 120, 64)` opaquely to the screen
at `(210,141)`, `(312,197)` or `(210,251)`, the slot's place on the screen.
Any other value leaves both rectangles uninitialised.

The handler slides the panel in with `fn_0041953E(0)`. When the gang's
`action` (record offset 7) is already 9 it takes the chosen slot from
`target` (offset 8), calls `fn_0044127B` for it and enables the confirm face
`(top=293, left=137, bottom=316, right=187)` through `fn_00418E66`
(`0x00440726`..`0x00440798`). In its event loop:

- Key down: Enter (`0x0D`) or Execute (`0x2B`) plays slot 4 when no slot is
  chosen; otherwise it presses the confirm face through `fn_00418CCC(0, ...)`,
  writes the chosen slot to `target` (`0x00440884`) and ends. Escape
  (`0x1B`) ends without an order after it presses the Cancel face `(top=261, left=137, bottom=284,
  right=187)` through `fn_00418CCC(1, ...)`.
- Left button down: outside `(104,124)-(448,333)` it plays slot 4
  (`0x0044092A`); inside it subtracts `(104,124)` (`0x00440970`) and tests
  the Cancel face, local `(33,137)-(82,159)`, and the confirm face, local
  `(33,169)-(82,191)`, which plays slot 4 while no slot is chosen.

## Interpretation

The panel is at `(104,124)` on the screen, with the shared command-panel
confirm and Cancel faces at its left edge. Each site of the gang's sector is
shown with a frame from `PX00129`; a site already completed is shown through
the pattern mask under a different frame and cannot be picked; the chosen site's picture is replaced by a version with
the selection frame. The layout is staggered: slots 0 and 2 on the left, slot
1 on the right between them. The test is equality, so a site whose progress
exceeded its Resistance would be drawn as selectable.

## Alternatives

- What the three 120-by-64 frames of `PX00129` look like has not been checked
  against the image.
- The pattern mask used by mode 0 is chosen by the resource (FND-PLATFORM-008);
  which pattern applies here has not been read.

## How to reproduce

In `0x00418F16`, find the loads of resources 2000 and 4999 into surface 5. In
`0x0043F692`, find the comparison of the word at `0x004AB67E` with the progress
byte at `0x004A08F0` (and `0x004A08F2`, `0x004A08F4`), the source rectangles
`(299,0x16A,0x16B,0x1E2)`, `(299,0xF2,0x16B,0x16A)` and `(0xEB,0,299,0x78)`,
and the destinations of top `0xA1`, `0xD9` and `0x10F`. In `0x0044127B`, find
the three pairs of rectangles passed to `0x0042773E` with surfaces 7 and 0.
