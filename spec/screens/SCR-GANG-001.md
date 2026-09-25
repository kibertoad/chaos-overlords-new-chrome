---
id: SCR-GANG-001
title: Compact gang information panel opened from the Attack, Equip, Research, Sell and Give panels
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-GANG-002, FND-GANG-004, FND-GANG-009, FND-GANG-010, FND-GANG-011, FND-GFX-006, FND-ATTACK-004, FND-GIVE-001, FND-RESEARCH-004, FND-SELL-001, FND-HIRE-008, FND-OPTIONS-001, FND-EXE-004]
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
| Name | The font of `fn_00413FD5` | The `name` of the gang's entry in `gang_definitions` | Starting at `(228, 151)` | While the panel is open | FND-GANG-002, FND-GANG-010 |
| Description | The font of `fn_00413FD5` | The `description` of the gang's definition, in three rows of 30 characters | Starting at `(228, 169)`, `(228, 178)` and `(228, 187)` | While the panel is open | FND-GANG-002, FND-GANG-010 |
| Force and left column | The digits of `DATA/PX16/PX00129` | Force on row 216 (two question marks in the font of `fn_00413FD5` when `force` is 0), Combat and Defense on rows 243 and 252, and Chaos, Control, Heal, Influence and Research on rows 270, 279, 288, 297 and 306, each drawn by `number_cells` (RULE-UI-004) in two glyph cells | Fields starting at x = 300 | While the panel is open | FND-GANG-002, FND-GANG-004, FND-GANG-009, FND-GANG-010 |
| Upkeep, Tech Level and right column | The digits of `DATA/PX16/PX00129` | The definition's `upkeep`, negated, on row 216, its `tech_level` on row 225, Stealth and Detect on rows 243 and 252, and Strength, Blade, Ranged, Fighting and Martial Arts on rows 270 to 306, each drawn by `number_cells` (RULE-UI-004) in two glyph cells | Fields starting at x = 396 | While the panel is open | FND-GANG-002, FND-GANG-004, FND-GANG-009, FND-GANG-010 |
| Base values | The digits of `DATA/PX16/PX00129`, then black drawn through bitmap 143 (rows `0x55` and `0xAA`), the pattern the grey 0x7FFF selects | The definition's own values of the fourteen statistics, with every other pixel black | x 282 and x 378, on the rows of the effective values; the pattern covers `(282, 243)-(294, 261)`, `(378, 243)-(390, 261)`, `(282, 270)-(294, 315)` and `(378, 270)-(390, 315)`, starting at each area's top-left corner, whose own pixel is black | While `pref_base_stats` is set | FND-GANG-009, FND-GANG-010, FND-GANG-011, FND-GFX-006 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Exit face | `(161, 293, 49, 22)` | Always | Closes the panel when the button is released inside; a double-click there does the same | FND-GANG-002, FND-GANG-010 |
| Outside the panel | Outside `(128, 124, 320, 209)` | Always | Refused with slot 4 | FND-GANG-010 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| `Enter` or `Execute` (virtual key `0x2B`) | Always | Presses the exit face and closes the panel; `Escape` and other keys do nothing | FND-GANG-002, FND-GANG-010 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Panel opening | General effect slot 0, as RULE-UI-003 gives | The panel slides in, with Slide Panels on | FND-OPTIONS-001 |
| Panel closing | General effect slot 1, as RULE-UI-003 gives | The panel slides out, with Slide Panels on | FND-OPTIONS-001 |
| Refused | General effect slot 4 | A press or double-click outside the panel | FND-GANG-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | A gang portrait is double-clicked on the Attack panel (the acting gang or a target), the Equip, Research or Sell panel (the acting gang) or the Give panel (the giver or a recipient); the panel gets a copy of that gang's record | The exit face is clicked, or `Enter` or `Execute` pressed | FND-GANG-002, FND-GANG-010 |

## Timing

The panel slides in and out in the alternate 320-pixel form, in about a
quarter of a second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- While open, the panel keeps the left 24 columns of the order panel it was
  opened from on screen at `(104, 124)` and redraws them on repaint
  (FND-GANG-010).
