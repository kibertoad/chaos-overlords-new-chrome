---
id: SCR-GANG-001
title: Gang definition panel for a hire offer
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-GANG-002, FND-GANG-004, FND-OPTIONS-001]
conflicting: []
split_with: []
related: [RULE-UI-003, RULE-UI-004]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05022`, the 320-pixel part from buffer `(344,144)` | None | `(128, 124, 320, 209)` | While the panel is open | FND-GANG-002 |
| Portrait | `DATA/PX08/Px03000` | The offered gang's 64-by-64 portrait | `(154, 141, 64, 64)` | While the panel is open | FND-GANG-002 |
| Name | Not recorded | The `name` of the gang's entry in `gang_definitions` | Starting at `(228, 151)` | While the panel is open | FND-GANG-002 |
| Description | Not recorded | The `description` of the gang's definition, in three rows of 30 characters | Starting at `(228, 169)`, `(228, 178)` and `(228, 187)` | While the panel is open | FND-GANG-002 |
| Force and left column | Not recorded | Force, and the left column of statistics, each drawn by `number_cells` (RULE-UI-004) in two glyph cells | Fields starting at x = 300 | While the panel is open | FND-GANG-002, FND-GANG-004 |
| Upkeep, Tech Level and right column | Not recorded | The definition's `upkeep`, `tech_level` and the right column of statistics, each drawn by `number_cells` (RULE-UI-004) in two glyph cells | Fields starting at x = 396 | While the panel is open | FND-GANG-002, FND-GANG-004 |
| Statistic rows | Not recorded | Two statistics per row, one in each column | Rows at y = 243, 252, 270, 279, 288, 297 and 306 | While the panel is open | FND-GANG-002 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Exit control | `(161, 293, 49, 22)` | Always | Closes the panel | FND-GANG-002 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| The confirmation key (not named by the finding) | Always | Closes the panel | FND-GANG-002 |

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
| Open | A hire offer's definition is inspected | The exit control is clicked or the confirmation key pressed | FND-GANG-002 |

## Timing

The panel slides in and out in the alternate 320-pixel form, in about a
quarter of a second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- Which statistic sits on which row and column, and the rows of Force, Upkeep
  and Tech Level.
- Whether the Force marker for an offer is drawn on this panel as it is on
  `PX05000` (FND-GANG-004 records it for the live-gang handler).
- The font of the name and description, and the key that closes the panel.
- How the panel is opened from the hire offers is described by the hire
  screens.
