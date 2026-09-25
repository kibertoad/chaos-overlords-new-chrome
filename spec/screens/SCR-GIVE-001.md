---
id: SCR-GIVE-001
title: Give panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EQUIP-003, FND-OPTIONS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-GIVE-001, RULE-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05015` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-EQUIP-003, FND-OPTIONS-001 |
| Item pictures | Not recorded | The acting gang's weapon, armor and miscellaneous item, each one pixel inside its target cell | Inside `(207, 139, 52, 52)`, `(207, 203, 52, 52)` and `(207, 267, 52, 52)` | For each filled slot | FND-EQUIP-003 |
| Recipients | `DATA/PX08/Px03000` | Up to five eligible friendly gangs, in roster order | `(313, 140 + 36 * n, 32, 32)`, `n` from 0 to 4 | While the panel is open | FND-EQUIP-003 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Weapon cell | `(207, 139, 52, 52)` | The slot holds an eligible item | Toggles the weapon in the selection | FND-EQUIP-003 |
| Armor cell | `(207, 203, 52, 52)` | The slot holds an eligible item | Toggles the armor in the selection | FND-EQUIP-003 |
| Miscellaneous cell | `(207, 267, 52, 52)` | The slot holds an eligible item | Toggles the miscellaneous item in the selection | FND-EQUIP-003 |
| Recipient `n` | `(313, 140 + 36 * n, 32, 32)` | A recipient is shown there | Chooses that gang as the recipient | FND-EQUIP-003 |
| Cancel control | Not recorded | Always | Closes the panel without an order | FND-EQUIP-003 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| `Enter` | At least one eligible item and a recipient are selected | Stores the Give order, carried out by RULE-GIVE-001, and closes the panel | FND-EQUIP-003 |
| `Execute` (virtual key `0x2B`) | As for `Enter` | As for `Enter` | FND-EQUIP-003 |
| `Escape` | Always | Closes the panel without an order | FND-EQUIP-003 |

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
| Incomplete | The panel opens, or the selection loses its last item | Both an item and a recipient are selected, or the panel closes | FND-EQUIP-003 |
| Ready | At least one item and a recipient are selected | The last item is deselected, or the panel closes | FND-EQUIP-003 |

## Timing

The panel slides in and out as the shared panels do, in about a quarter of a
second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- What makes an item or a recipient eligible. SRC-MANUAL-GOG, page 31, says an
  item whose Tech Level is too high for the recipient is dimmed and cannot be
  given; whether recipients must be in the giver's sector is not recorded.
- The position of the Cancel control, whether a pointer control also confirms,
  and how the selected items and recipient are marked.
- The resources of the item pictures.
