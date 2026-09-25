---
id: SCR-GANG-002
title: Gang information panel for a hired gang
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-GANG-004, FND-OPTIONS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-GANG-001, RULE-UI-003, RULE-UI-004]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05000` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-GANG-004, FND-OPTIONS-001 |
| Left value column | Not recorded | Force and the left column of the fourteen effective statistics from RULE-GANG-001, each drawn by `number_cells` (RULE-UI-004) in two cells, which blanks the first cell of a one-digit value and draws a negative value's digits in red | Fields starting at x = 276 | While the panel is open | FND-GANG-004 |
| Right value column | Not recorded | Upkeep, negated before drawing, Tech Level and the right column of statistics, in the same two-cell fields | Fields starting at x = 372 | While the panel is open | FND-GANG-004 |
| Equipment | Not recorded | The gang's `weapon`, `armor` and `misc` | Not recorded | While the panel is open | SRC-MANUAL-GOG |

## Mouse input

None known.

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
| Open | A gang's information is opened | The panel is closed | FND-GANG-004 |

## Timing

The panel slides in and out as the shared panels do, in about a quarter of a
second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- The panel's position is inferred from the recorded buffer-to-screen mapping
  (buffer x 172 and 268 to screen x 276 and 372), which matches the shared
  panel's left edge of 104; its y offset is not recorded.
- The rows of each value, the portrait, name and equipment positions, and the
  controls that close the panel.
- How the panel is opened, and whether it can show an offered gang (with the
  two-character unknown marker for Force that FND-GANG-004 records) as well as
  a hired one.
