---
id: SCR-MOVE-001
title: Move panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-MOVE-002, FND-OPTIONS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-MOVE-001, RULE-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05006` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-MOVE-002, FND-OPTIONS-001 |
| Neighborhood | Not recorded | The acting gang's sector in the center and the eight sectors around it, three cells of 54 by 52 per row | `(236, 150, 162, 156)` | While the panel is open | FND-MOVE-002 |
| Acting gang | `DATA/PX08/Px03000` | The acting gang's portrait | Not recorded | While the panel is open | SRC-MANUAL-GOG |
| Direction arrow | Not recorded | The direction of the chosen move | Not recorded | Once a destination is chosen | SRC-MANUAL-GOG |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Neighborhood cell in column `c` and row `r`, `c` and `r` from 0 to 2 | `(236 + 54 * c, 150 + 52 * r, 54, 52)` | `c + 3 * r` is not 4, and the cell's sector lies inside the city | Chooses the sector at offset -9, -8, -7, -1, +1, +7, +8 or +9 from the gang's sector, in row-major order, as the destination | FND-MOVE-002 |
| Center cell | `(290, 202, 54, 52)` | Never | None | FND-MOVE-002 |
| Confirm control | Not recorded | A valid destination is chosen | Stores the Move order, carried out later by RULE-MOVE-001, and closes the panel | FND-MOVE-002 |
| Cancel control | Not recorded | Always | Closes the panel without an order | FND-MOVE-002 |

## Keyboard input

None known.

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Panel opening | General effect slot 0, as RULE-UI-003 gives | The panel slides in, with Slide Panels on | FND-OPTIONS-001 |
| Panel closing | General effect slot 1, as RULE-UI-003 gives | The panel slides out, with Slide Panels on | FND-OPTIONS-001 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| No destination | The panel opens | A valid cell is clicked, or the panel is cancelled | FND-MOVE-002 |
| Destination chosen | A valid cell is clicked | Another valid cell is clicked, or the panel is confirmed or cancelled | FND-MOVE-002 |

## Timing

The panel slides in from the right edge and out again. The step is worked out
from a copy benchmark taken at startup so that the slide lasts about a quarter
of a second, with at least 16 pixels a step (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- The positions of the Confirm and Cancel controls: FND-MOVE-002 says only that
  they are the shared panel's common controls.
- What draws the neighborhood (which city layers, and how the center sector is
  marked), where the acting gang's portrait sits, and what marks the chosen
  destination.
- Which keys the panel accepts.
- Whether the panel refuses a sector that already holds six of the player's
  gangs, or leaves that to RULE-MOVE-002.
- Which of `DATA/PX08/PX05006` and `DATA/PX16/PX05006` is drawn depends on the
  display mode; the entries here name the `PX08` path the executable's template
  uses.
