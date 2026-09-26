---
id: SCR-EQUIP-001
title: Equip panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EQUIP-005, FND-EQUIP-009, FND-EQUIP-010, FND-EQUIP-006, FND-EQUIP-008, FND-EQUIP-001, FND-OPTIONS-001, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-EQUIP-001, RULE-EQUIP-003, RULE-EQUIP-004, RULE-UI-003, SCR-GANG-001, SCR-UI-006]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05004` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-EQUIP-005, FND-OPTIONS-001 |
| Gang portrait | `DATA/PX08/Px03000` 64-by-64 cell of the gang's definition | None | `(130, 141, 64, 64)` | While the panel is open | FND-EQUIP-010 |
| Carried item icons | `DATA/PX16/PX04999` 20-by-20 cells | The gang's `weapon`, `armor` and `misc` | `(130, 206, 20, 20)`, `(152, 206, 20, 20)`, `(174, 206, 20, 20)` | For each item that is not -1 | FND-EQUIP-010 |
| Item list | The font of `fn_00413FD5`; the price in two number cells | The entries of RULE-EQUIP-004 for the chosen category, one per row of 9 pixels starting at y = 150, each with its price from `item_price` (RULE-EQUIP-003) | `(252, 150, 180, 143)` | While the panel is open | FND-EQUIP-005, FND-EQUIP-006, FND-EQUIP-001 |
| Category frame | `DATA/PX16/PX00129` crop `(120,171,34,34)`, keyed on exact white | The chosen category | `(207, 139 + 36 * n, 34, 34)`, one pixel outside category cell `n` | Always; the panel opens on category 0, or on the category of the item in `target` when the gang's `action` is already Equip | FND-EQUIP-009 |
| Chosen row mark | The row's 30-character text in the second font of `DATA/PX16/PX00129` (glyph row y 441), inside a one-pixel frame of the colour `(0, 255, 0)`, composed above the panel in the work surface | The item currently chosen; the strip covers the row's price | `(251, 149 + 9 * n, 181, 9)` for row `n` | Once an item is chosen, and on opening when the gang's pending item is listed | FND-EQUIP-010 |
| Confirm face | Enabled or disabled state drawn by `fn_00418E66` | Whether the order can be confirmed | `(137, 293, 50, 23)` | Drawn enabled when a row is chosen and on opening when the gang already has an Equip order, disabled after a category change or a click on an empty row; not drawn on opening otherwise | FND-EQUIP-010 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Category cell 0 | `(208, 140, 32, 32)` | Always | Clears the chosen item and rebuilds the list for category 0 (RULE-EQUIP-004) | FND-EQUIP-005 |
| Category cell 1 | `(208, 176, 32, 32)` | Always | The same for category 1 | FND-EQUIP-005 |
| Category cell 2 | `(208, 212, 32, 32)` | Always | The same for category 2 | FND-EQUIP-005 |
| Category cell 3 | `(208, 248, 32, 32)` | Always | The same for category 3 | FND-EQUIP-005 |
| Item list row `n`, `n` from 0 to 15 | `(252, 150 + 9 * n, 180, 9)`, with the last row cut off at y = 293 | The row holds an item | Chooses that item | FND-EQUIP-005 |
| Confirm face | `(137, 293, 49, 22)` | An item is chosen | Stores the Equip order with the chosen item, carried out by RULE-EQUIP-001, and closes the panel when the button is released inside; no cash test is made; refused with slot 4 otherwise | FND-EQUIP-006, FND-EQUIP-010 |
| Cancel face | `(137, 261, 49, 22)` | Always | Closes the panel without an order when the button is released inside | FND-EQUIP-005, FND-EQUIP-010 |
| Outside the panel | Outside `(104, 124, 344, 209)` | Always | Refused with slot 4 | FND-EQUIP-010 |
| Item list row, double-click | As the rows | The row holds an item | Opens SCR-UI-006 for the item | FND-EQUIP-010 |
| Carried item icon, double-click | `(130, 206, 20, 20)`, `(152, 206, 20, 20)`, `(174, 206, 20, 20)` | The slot holds an item | Opens SCR-UI-006 for the item | FND-EQUIP-010 |
| Portrait, double-click | `(130, 141, 64, 64)` | Always | Opens SCR-GANG-001 for the gang | FND-EQUIP-010 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| `Enter` or `Execute` (virtual key `0x2B`) | An item is chosen | As the Confirm face; refused with slot 4 otherwise | FND-EQUIP-006, FND-EQUIP-010 |
| `Escape` | Always | As the Cancel face | FND-EQUIP-010 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Panel opening | General effect slot 0, as RULE-UI-003 gives | The panel slides in, with Slide Panels on | FND-OPTIONS-001 |
| Panel closing | General effect slot 1, as RULE-UI-003 gives | The panel slides out, with Slide Panels on | FND-OPTIONS-001 |
| Refused | General effect slot 4 | A press outside the panel, or a confirm with no item chosen | FND-EQUIP-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Category shown, no item chosen | The panel opens, or a category cell is clicked | An item row is clicked, or the panel closes | FND-EQUIP-005 |
| Item chosen | An item row is clicked, or the panel opens for a gang whose pending Equip item is listed | A category cell or an empty row is clicked, or the panel closes | FND-EQUIP-005, FND-EQUIP-010 |

## Timing

The panel slides in and out as the shared panels do, in about a quarter of a
second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- The list rows are 9 pixels high from y = 150, so the pointer area's height of
  143 cuts the sixteenth row to 8 pixels.
- When the gang's pending item is no longer listed, the panel opens with the
  confirm face drawn enabled and no row chosen, and a confirm is refused
  (FND-EQUIP-010).
- The look of the glyphs at y 441 of `PX00129` has not been checked against the
  image.
