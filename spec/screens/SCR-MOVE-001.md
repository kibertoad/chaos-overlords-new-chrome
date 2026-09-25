---
id: SCR-MOVE-001
title: Move panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-MOVE-002, FND-MOVE-004, FND-MOVE-005, FND-OPTIONS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-MOVE-001, RULE-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05006` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-MOVE-002, FND-OPTIONS-001 |
| Neighborhood | A 162-by-156 crop of the drawn city map, surface 2 | The acting gang's sector in the center and the eight sectors around it, three cells of 54 by 52 per row; the gang's own map cell starts one pixel right of and below the middle cell's corner | `(236, 150, 162, 156)` | While the panel is open | FND-MOVE-002, FND-MOVE-004 |
| Off-city bands | Black fill | Grid cells beyond the city's edge | The top or bottom 52 rows of the grid when the sector is in row 0 or 7; the left or right 54 columns when it is in column 0 or 7 | For each edge the sector touches | FND-MOVE-004 |
| Acting gang | `DATA/PX08/Px03000` 64-by-64 cell of the definition; the area `(414,363,64,64)` of the interface sheet when the record's `definition` is -1 | The acting gang's portrait | `(130, 141, 64, 64)` | While the panel is open | SRC-MANUAL-GOG, FND-MOVE-004 |
| Direction arrow | The interface sheet `PX00129`, 32-by-32 cell `(32 * i, 448)` keyed on exact white, for index `i` 0 to 7 of the offsets -9, -8, -7, -1, +1, +7, +8, +9 | The direction of the chosen move | `(273,185)`, `(302,177)`, `(329,185)`, `(265,212)`, `(337,212)`, `(273,238)`, `(302,246)`, `(329,238)` for `i` 0 to 7, each 32 by 32, around the centre cell | Once a destination is chosen, and on opening when the gang already has a Move order | FND-MOVE-004, FND-MOVE-005 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Neighborhood cell in column `c` and row `r`, `c` and `r` from 0 to 2 | `(236 + 54 * c, 150 + 52 * r, 54, 52)` | `c + 3 * r` is not 4, and the cell's entry in the nine-byte table at `0x004ABC40` is nonzero | Chooses the sector at offset -9, -8, -7, -1, +1, +7, +8 or +9 from the selected sector, in row-major order, as the destination; a double-click does the same | FND-MOVE-002, FND-MOVE-004 |
| Center cell | `(290, 202, 54, 52)` | Never | None | FND-MOVE-002 |
| Confirm face | `(137, 293, 49, 22)` | A valid destination is chosen | Stores the Move order, carried out later by RULE-MOVE-001, and closes the panel when the button is released inside; refused with slot 4 otherwise | FND-MOVE-002, FND-MOVE-004 |
| Cancel face | `(137, 261, 49, 22)` | Always | Closes the panel without an order when the button is released inside | FND-MOVE-002, FND-MOVE-004 |
| Outside the panel | Outside `(104, 124, 344, 209)` | Always | Refused with slot 4 | FND-MOVE-004 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| `Enter` or `Execute` (virtual key `0x2B`) | A valid destination is chosen | As the Confirm face; refused with slot 4 otherwise | FND-MOVE-004 |
| `Escape` | Always | As the Cancel face | FND-MOVE-004 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Panel opening | General effect slot 0, as RULE-UI-003 gives | The panel slides in, with Slide Panels on | FND-OPTIONS-001 |
| Panel closing | General effect slot 1, as RULE-UI-003 gives | The panel slides out, with Slide Panels on | FND-OPTIONS-001 |
| Refused | General effect slot 4 | A press outside the panel, or a confirm with no destination | FND-MOVE-004 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| No destination | The panel opens for a gang whose action is not Move | A valid cell is clicked, or the panel is cancelled | FND-MOVE-002, FND-MOVE-004 |
| Destination chosen | A valid cell is clicked, or the panel opens for a gang whose action is already Move (the stored `target`) | Another valid cell is clicked, or the panel is confirmed or cancelled | FND-MOVE-002 |

## Timing

The panel slides in from the right edge and out again. The step is worked out
from a copy benchmark taken at startup so that the slide lasts about a quarter
of a second, with at least 16 pixels a step (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- Which function fills the nine-byte table at `0x004ABC40` that disables cells.
- The destination is the selected sector plus the offset (FND-MOVE-004); when
  the panel is opened from `fn_0041462F` rather than the gang's command box,
  whether the selected sector is always the gang's own has not been checked.
- Whether the panel refuses a sector that already holds six of the player's
  gangs, or leaves that to RULE-MOVE-002.
- What the eight arrow cells look like has not been checked against the
  image (FND-MOVE-005).
- Which of `DATA/PX08/PX05006` and `DATA/PX16/PX05006` is drawn depends on the
  display mode; the entries here name the `PX08` path the executable's template
  uses.
