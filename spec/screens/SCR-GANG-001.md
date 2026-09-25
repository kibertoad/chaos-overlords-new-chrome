---
id: SCR-GANG-001
title: Compact gang information panel opened from the Attack, Equip, Research, Sell and Give panels
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-GANG-002, FND-GANG-004, FND-GANG-009, FND-ATTACK-004, FND-GIVE-001, FND-RESEARCH-004, FND-SELL-001, FND-HIRE-008, FND-OPTIONS-001, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-UI-003, RULE-UI-004, SCR-ATTACK-001, SCR-EQUIP-001, SCR-RESEARCH-001, SCR-SELL-001, SCR-GIVE-001, SCR-GANG-002]
---

## Drawn elements

The panel shows a gang that exists, passed as a copy of its record by the
Attack, Equip, Research, Sell or Give panel. It was first read as the panel for
a hire offer's definition (FND-GANG-002); a hire offer opens SCR-GANG-002
instead (FND-GANG-009, FND-HIRE-008).

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05022`, the 320-pixel part from buffer `(344,144)` | None | `(128, 124, 320, 209)` | While the panel is open | FND-GANG-002 |
| Portrait | `DATA/PX08/Px03000` | The 64-by-64 portrait of the gang's definition | `(154, 141, 64, 64)` | While the panel is open | FND-GANG-002 |
| Name | Not recorded | The `name` of the gang's entry in `gang_definitions` | Starting at `(228, 151)` | While the panel is open | FND-GANG-002 |
| Description | Not recorded | The `description` of the gang's definition, in three rows of 30 characters | Starting at `(228, 169)`, `(228, 178)` and `(228, 187)` | While the panel is open | FND-GANG-002 |
| Force and left column | Not recorded | The gang's current `force`, or a question-mark string when it is 0, and the left column of the gang's effective statistics, each drawn by `number_cells` (RULE-UI-004) in two glyph cells | Fields starting at x = 300 | While the panel is open | FND-GANG-002, FND-GANG-004, FND-GANG-009 |
| Upkeep, Tech Level and right column | Not recorded | The definition's `upkeep` as a negative number, its `tech_level`, and the right column of the gang's effective statistics, each drawn by `number_cells` (RULE-UI-004) in two glyph cells | Fields starting at x = 396 | While the panel is open | FND-GANG-002, FND-GANG-004, FND-GANG-009 |
| Base values | Not recorded | The definition's own values of the statistics, in a further column | Not recorded | While `pref_base_stats` is set | FND-GANG-009 |
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
| Open | A gang portrait is double-clicked on the Attack panel (the acting gang or a target), the Equip, Research or Sell panel (the acting gang) or the Give panel (the giver or a recipient); the panel gets a copy of that gang's record | The exit control is clicked or the confirmation key pressed | FND-GANG-002 |

## Timing

The panel slides in and out in the alternate 320-pixel form, in about a
quarter of a second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- Which statistic sits on which row and column, and the rows of Force, Upkeep
  and Tech Level.
- The font of the name and description, and the key that closes the panel.
