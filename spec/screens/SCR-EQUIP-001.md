---
id: SCR-EQUIP-001
title: Equip panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EQUIP-005, FND-EQUIP-009, FND-EQUIP-006, FND-EQUIP-001, FND-OPTIONS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-EQUIP-001, RULE-EQUIP-003, RULE-EQUIP-004, RULE-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05004` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-EQUIP-005, FND-OPTIONS-001 |
| Item list | Not recorded | The entries of RULE-EQUIP-004 for the chosen category, one per row of 9 pixels starting at y = 150, each with its price from `item_price` (RULE-EQUIP-003) | `(252, 150, 180, 143)` | While the panel is open | FND-EQUIP-005, FND-EQUIP-006, FND-EQUIP-001 |
| Category frame | `DATA/PX16/PX00129` crop `(120,171,34,34)`, keyed on exact white | The chosen category | `(207, 139 + 36 * n, 34, 34)`, one pixel outside category cell `n` | Always; the panel opens on category 0, or on the category of the item in `target` when the gang's `action` is already Equip | FND-EQUIP-009 |
| Chosen row mark | Not recorded | The item currently chosen | Not recorded | Once an item is chosen | FND-EQUIP-005 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Category cell 0 | `(208, 140, 32, 32)` | Always | Clears the chosen item and rebuilds the list for category 0 (RULE-EQUIP-004) | FND-EQUIP-005 |
| Category cell 1 | `(208, 176, 32, 32)` | Always | The same for category 1 | FND-EQUIP-005 |
| Category cell 2 | `(208, 212, 32, 32)` | Always | The same for category 2 | FND-EQUIP-005 |
| Category cell 3 | `(208, 248, 32, 32)` | Always | The same for category 3 | FND-EQUIP-005 |
| Item list row `n`, `n` from 0 to 15 | `(252, 150 + 9 * n, 180, 9)`, with the last row cut off at y = 293 | The row holds an item | Chooses that item | FND-EQUIP-005 |
| Confirm control | Not recorded | An item is chosen | Stores the Equip order with the chosen item, carried out by RULE-EQUIP-001; no cash test is made | FND-EQUIP-006 |
| Cancel control | Not recorded | Always | Closes the panel without an order | FND-EQUIP-005 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| The confirmation key (not named by the findings) | An item is chosen | Stores the Equip order with the chosen item, as the Confirm control does | FND-EQUIP-006 |

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
| Category shown, no item chosen | The panel opens, or a category cell is clicked | An item row is clicked, or the panel closes | FND-EQUIP-005 |
| Item chosen | An item row is clicked | A category cell is clicked, or the panel closes | FND-EQUIP-005 |

## Timing

The panel slides in and out as the shared panels do, in about a quarter of a
second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- Which items each category cell lists: item types 0 and 1 map to cell 0, 2
  to 1, 3 to 2 and 4 to 3 in the handler and in the Research list builder
  (FND-EQUIP-009); the Equip list builder `fn_0043F136` has not been read for
  the mapping.
- The list rows are 9 pixels high from y = 150, so the pointer area's height of
  143 cuts the sixteenth row to 8 pixels.
- The positions of the Confirm and Cancel controls, the key that confirms, and
  the font and columns of the list.
- Whether a double-click on a row opens the Item Information panel.
