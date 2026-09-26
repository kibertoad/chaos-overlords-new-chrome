---
id: SCR-GANG-002
title: Gang information panel for a hired gang
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-GANG-004, FND-GANG-006, FND-GANG-008, FND-OPTIONS-001, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-GANG-001, RULE-UI-003, RULE-UI-004, SCR-UI-006]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05000` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-GANG-004, FND-GANG-006, FND-OPTIONS-001 |
| Portrait | `DATA/PX08/Px03000` 64-by-64 cell of the gang's definition | None | `(130, 141, 64, 64)` | While the panel is open | FND-GANG-006 |
| Name and description | The font of `fn_00413FD5` | The definition's name and its three 30-character description lines | From `(204, 151)`; description rows 169, 178 and 187 | While the panel is open | FND-GANG-006 |
| Left value column | The digits of `DATA/PX16/PX00129` | Force on row 216 (two question marks when `force` is 0), Combat and Defense on rows 243 and 252, and Chaos, Control, Heal, Influence and Research on rows 270, 279, 288, 297 and 306, each drawn in two cells (RULE-UI-004), which blanks the first cell of a one-digit value and draws a negative value's digits in red | Fields starting at x = 276 | While the panel is open | FND-GANG-004, FND-GANG-006 |
| Right value column | The digits of `DATA/PX16/PX00129` | Upkeep, negated before drawing, on row 216, Tech Level on row 225, Stealth and Detect on rows 243 and 252, and Strength, Blade, Ranged, Fighting and Martial Arts on rows 270 to 306 | Fields starting at x = 372 | While the panel is open | FND-GANG-004, FND-GANG-006 |
| Base values | The digits drawn by `fn_0041B668` | The definition's base value of each of the fourteen statistics | x 258 and x 354, on the rows of the effective value | When `pref_base_stats` is set | FND-GANG-006 |
| Equipment | The item's strip `PX04xxx`, 15 frames of 48 by 48 | The gang's `weapon`, `armor` and `misc`, animated | `(392, 141 + 64 * k, 48, 48)` for slot `k` | For each filled slot | SRC-MANUAL-GOG, FND-GANG-006 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Close face | `(137, 293, 49, 22)` | Always | Closes the panel when the button is released inside; a double-click there does the same | FND-GANG-006 |
| Equipment picture, double-click | `(391, 140 + 64 * k, 50, 50)` | The slot holds an item | Opens SCR-UI-006 for the item | FND-GANG-006 |
| Outside the panel | Outside `(104, 124, 344, 209)` | Always | Refused with slot 4 | FND-GANG-006 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| `Enter` or `Execute` (virtual key `0x2B`) | Always | Closes the panel | FND-GANG-006 |

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
| Open | A gang's information is opened: from the gang command box `fn_00414D8C`, from `fn_004169B3`, or from the Hire input handler for an offer, which passes a record with Force 0, no items and sector 100, so Force shows two question marks | The panel is closed | FND-GANG-004, FND-GANG-006, FND-GANG-008 |

## Timing

The panel slides in and out as the shared panels do, in about a quarter of a
second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- Screen positions follow from the handler's buffer positions through the
  shared panel's mapping, screen = buffer + (104, -20) (FND-GANG-006); they have
  not been checked against a capture.
- Whether callers other than the Hire input handler can pass a record whose
  `force` is 0 has not been checked (FND-GANG-008).
