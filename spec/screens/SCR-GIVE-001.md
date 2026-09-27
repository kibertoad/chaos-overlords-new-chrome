---
id: SCR-GIVE-001
title: Give panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EQUIP-003, FND-GIVE-001, FND-GIVE-002, FND-GIVE-003, FND-GFX-006, FND-OPTIONS-001, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-GIVE-001, RULE-UI-003, SCR-GANG-001, SCR-UI-006]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX08/PX05015` | None | `(104, 124, 344, 209)`, the shared panel position | While the panel is open | FND-EQUIP-003, FND-OPTIONS-001 |
| Giver portrait | `DATA/PX08/Px03000` 64-by-64 cell of the giver's definition | None | `(130, 141, 64, 64)` | While the panel is open | FND-GIVE-001 |
| Item pictures | The item's 720-by-48 strip `PX04xxx` (resource 4000 plus the item's `id`), 15 frames of 48 by 48 | The acting gang's weapon, armor and miscellaneous item, each one pixel inside its target cell, animated | `(209, 141, 48, 48)`, `(209, 205, 48, 48)` and `(209, 269, 48, 48)` | For each filled slot | FND-EQUIP-003, FND-GIVE-001 |
| Recipient list | None: nothing fills the list area, so the panel image shows where no card is drawn | None | `(312, 139, 97, 178)` | While the panel is open | FND-GIVE-003 |
| Recipient card | `DATA/PX16/PX00129` card `(0,560,97,34)` | Up to five of the player's other gangs in the giver's sector, in roster order | `(312, 139 + 36 * n, 97, 34)`, `n` from 0 to 4 | While the panel is open | FND-EQUIP-003, FND-GIVE-001, FND-GIVE-002 |
| Recipient portrait | `DATA/PX16/PX03000` 64-by-64 cell of the gang's definition, scaled to 32 by 32 | None | `(313, 140 + 36 * n, 32, 32)` | For each card | FND-GIVE-002 |
| Recipient Force meter | `DATA/PX16/PX00129` green strip `(354,0)`, `6 * force` pixels long, 3 rows | The gang's `force` | From `(347, 143 + 36 * n)` | For each card | FND-GIVE-002 |
| Recipient item icons | `DATA/PX16/PX04999` 20-by-20 cells | The gang's `weapon`, `armor` and `misc` | `(346, 150 + 36 * n)`, `(367, 150 + 36 * n)`, `(388, 150 + 36 * n)`, each 20 by 20 | For each item that is not -1 | FND-GIVE-002 |
| Dimmed card | Black drawn through bitmap 146 (rows `0x88` and `0x22`), the pattern the grey 48,000 selects, so 48 of every 64 pixels turn black | A recipient whose gang definition's `tech_level` is below the highest Tech Level of the selected items | Over the 97-by-34 card, the pattern starting at its top-left corner, whose own pixel keeps the card | While that holds | FND-GIVE-002, FND-GIVE-003, FND-GFX-006 |
| Item selection frame | `DATA/PX16/PX00129` crop `(414,13,54,54)`, keyed on exact white | Which items are selected | `(206, 138 + 64 * r, 54, 54)` for row `r` | For each selected item | FND-GIVE-002 |
| Recipient marker | `DATA/PX16/PX00129` crop `(128,448,32,32)`, keyed on exact white, the arrow the Move panel uses for +1 | The chosen recipient | `(274, 140 + 36 * n, 32, 32)`, left of card `n` | Once a recipient is chosen | FND-GIVE-002 |
| Confirm face | Enabled or disabled state drawn by `fn_00418E66` | Whether the order can be confirmed | `(137, 293, 50, 23)` | Enabled in Ready, and on opening when the gang already has a Give order | FND-GIVE-001 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Weapon cell | `(207, 139, 52, 52)` | The slot holds an eligible item | Toggles the weapon in the selection | FND-EQUIP-003 |
| Armor cell | `(207, 203, 52, 52)` | The slot holds an eligible item | Toggles the armor in the selection | FND-EQUIP-003 |
| Miscellaneous cell | `(207, 267, 52, 52)` | The slot holds an eligible item | Toggles the miscellaneous item in the selection | FND-EQUIP-003 |
| Recipient `n` | `(312, 139 + 36 * n, 97, 34)` | A recipient is listed there and its definition's Tech Level is at least the highest Tech Level of the selected items | Chooses that gang as the recipient | FND-GIVE-001 |
| Cancel face | `(137, 261, 49, 22)` | Always | Closes the panel without an order when the button is released inside | FND-GIVE-001 |
| Confirm face | `(137, 293, 49, 22)` | Ready | Stores the Give order and closes the panel when the button is released inside; refused with slot 4 otherwise | FND-GIVE-001 |
| Outside the panel | Outside `(104, 124, 344, 209)` | Always | Refused with slot 4 | FND-GIVE-001 |
| Item cell, double-click | As the item cells | The slot holds an item | Opens SCR-UI-006 for the item | FND-GIVE-001 |
| Giver portrait, double-click | `(130, 141, 64, 64)` | Always | Opens SCR-GANG-001 for the giver | FND-GIVE-001 |
| Recipient portrait, double-click | `(313, 140 + 36 * n, 32, 32)` | A recipient is listed there | Opens SCR-GANG-001 for that gang | FND-GIVE-001 |
| Recipient item icon, double-click | x `346`, `367` or `388`, y `150 + 36 * n`, 20 by 20 | The recipient holds an item in that slot | Opens SCR-UI-006 for that item | FND-GIVE-001 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| `Enter` | Ready | Stores the Give order, carried out by RULE-GIVE-001, and closes the panel; refused with slot 4 otherwise | FND-EQUIP-003, FND-GIVE-001 |
| `Execute` (virtual key `0x2B`) | As for `Enter` | As for `Enter` | FND-EQUIP-003 |
| `Escape` | Always | Closes the panel without an order | FND-EQUIP-003, FND-GIVE-001 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Panel opening | General effect slot 0, as RULE-UI-003 gives | The panel slides in, with Slide Panels on | FND-OPTIONS-001 |
| Panel closing | General effect slot 1, as RULE-UI-003 gives | The panel slides out, with Slide Panels on | FND-OPTIONS-001 |
| Refused | General effect slot 4 | A press outside the panel, or a confirm while not Ready | FND-GIVE-001 |

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

- The art of the frames and the marker has not been checked against the image
  (FND-GIVE-002).
- The handler stores every gang of the player in the giver's sector in a
  five-entry list without a count check (FND-GIVE-001); whether more than five
  can qualify is not recorded.
