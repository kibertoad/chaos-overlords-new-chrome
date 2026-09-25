---
id: SCR-SELL-001
title: Sell panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EQUIP-004, FND-EXE-004, FND-SELL-001, FND-SELL-002, FND-OPTIONS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-SELL-001, RULE-UI-003, SCR-GANG-001, SCR-UI-006]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/Px05013` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-EQUIP-004, FND-OPTIONS-001 |
| Gang portrait | `DATA/PX08/Px03000` 64-by-64 cell of the gang's definition | None | `(130, 141, 64, 64)` | While the panel is open | FND-SELL-001 |
| Item pictures | The item's strip `PX04xxx` (resource 4000 plus the item's `id`), 15 frames of 48 by 48 | The carried item, animated | `(217, 141 + 64 * k, 48, 48)` for slot `k` | For each filled slot | FND-SELL-001 |
| Item name | The font of `fn_00413FD5` | The item's name | From `(272, 156 + 64 * k)` | For each filled slot | FND-SELL-001 |
| Sale price | Two-cell number (`fn_00414187`) | Half the item's `cost`, rounded down | From `(386, 174 + 64 * k)` | For each filled slot | FND-SELL-001 |
| Empty row | Black fill | None | `(272, 149 + 64 * k, 125, 32)` | For each empty slot | FND-SELL-001 |
| Selection highlight | `DATA/PX16/PX00129` crop `(222,363,192,54)`, keyed on exact white; an unselected row gets the panel's own pixels back | Which rows are selected | `(214, 138 + 64 * k, 192, 54)`, one pixel outside row `k`'s input rectangle | For each selected row | FND-SELL-001, FND-SELL-002 |
| OK face | Enabled or disabled state drawn by `fn_00418E66` | Whether the order can be confirmed | `(137, 293, 50, 23)` | Enabled while a row is selected, and on opening when the gang already has a Sell order | FND-SELL-001 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Weapon row | `(215, 139, 190, 52)` | The gang has a weapon | Toggles the weapon in the selection | FND-EQUIP-004 |
| Armor row | `(215, 203, 190, 52)` | The gang has armor | Toggles the armor in the selection | FND-EQUIP-004 |
| Miscellaneous row | `(215, 267, 190, 52)` | The gang has a miscellaneous item | Toggles it in the selection | FND-EQUIP-004 |
| OK face | `(137, 293, 49, 22)` | At least one row is selected | Stores the Sell order with the selection, carried out by RULE-SELL-001, and closes the panel when the button is released inside; refused with slot 4 otherwise | FND-EQUIP-004, FND-SELL-001 |
| Cancel face | `(137, 261, 49, 22)` | Always | Closes the panel without an order when the button is released inside | FND-EQUIP-004, FND-SELL-001 |
| Outside the panel | Outside `(104, 124, 344, 209)` | Always | Refused with slot 4 | FND-SELL-001 |
| Item row, double-click | As the rows | The slot holds an item | Opens SCR-UI-006 for the item | FND-SELL-001 |
| Portrait, double-click | `(130, 141, 64, 64)` | Always | Opens SCR-GANG-001 for the gang's definition | FND-SELL-001 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| `Enter` or `Execute` (virtual key `0x2B`) | At least one row is selected | As the OK face; refused with slot 4 otherwise | FND-SELL-001 |
| `Escape` | Always | As the Cancel face | FND-SELL-001 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Panel opening | General effect slot 0, as RULE-UI-003 gives | The panel slides in, with Slide Panels on | FND-OPTIONS-001 |
| Panel closing | General effect slot 1, as RULE-UI-003 gives | The panel slides out, with Slide Panels on | FND-OPTIONS-001 |
| Refused | General effect slot 4 | A press outside the panel, or OK with nothing selected | FND-SELL-001 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Nothing selected | The panel opens, or the last selected row is toggled off | A filled row is toggled on, or the panel closes | FND-EQUIP-004 |
| Selection made | A filled row is toggled on | The last selected row is toggled off, or the panel closes | FND-EQUIP-004 |

## Timing

The panel slides in and out as the shared panels do, in about a quarter of a
second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- What the highlight art looks like has not been checked against the image
  (FND-SELL-002). The item animation redraws its 48-by-48 frames over the
  highlight.
- The panel draws half of each item's `cost`, rounded down (FND-SELL-001).
  Selling several rows pays for only one of them (BUG-SELL-001).
