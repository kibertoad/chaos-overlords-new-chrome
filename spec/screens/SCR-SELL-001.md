---
id: SCR-SELL-001
title: Sell panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EQUIP-004, FND-OPTIONS-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-SELL-001, RULE-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/Px05013` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-EQUIP-004, FND-OPTIONS-001 |
| Item rows | Not recorded | For each of the weapon, armor and miscellaneous slots, the item and half its listed Cost | Rows wider than the targets below, on the same 64-pixel pitch | For each filled slot | FND-EQUIP-004, SRC-MANUAL-GOG |
| Selection highlight | Not recorded | Which rows are selected | Not recorded | For each selected row | SRC-MANUAL-GOG |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Weapon row | `(215, 139, 190, 52)` | The gang has a weapon | Toggles the weapon in the selection | FND-EQUIP-004 |
| Armor row | `(215, 203, 190, 52)` | The gang has armor | Toggles the armor in the selection | FND-EQUIP-004 |
| Miscellaneous row | `(215, 267, 190, 52)` | The gang has a miscellaneous item | Toggles it in the selection | FND-EQUIP-004 |
| OK control | Not recorded | At least one row is selected | Stores the Sell order with the selection, carried out by RULE-SELL-001, and closes the panel | FND-EQUIP-004 |
| Cancel control | Not recorded | Always | Closes the panel without an order | FND-EQUIP-004 |

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
| Nothing selected | The panel opens, or the last selected row is toggled off | A filled row is toggled on, or the panel closes | FND-EQUIP-004 |
| Selection made | A filled row is toggled on | The last selected row is toggled off, or the panel closes | FND-EQUIP-004 |

## Timing

The panel slides in and out as the shared panels do, in about a quarter of a
second (FND-OPTIONS-001), as RULE-UI-003 describes.

## Differences between builds

None known.

## Open questions

- The positions of the OK and Cancel controls, the keys the panel accepts, and
  how the rows and the highlight are drawn.
- The price drawn on each row is taken from the manual (half the original
  price); which value the panel draws is not recorded. Selling several rows pays
  for only one of them (BUG-SELL-001).
