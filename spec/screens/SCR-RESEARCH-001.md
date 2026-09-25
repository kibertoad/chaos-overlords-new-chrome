---
id: SCR-RESEARCH-001
title: Research panel with item categories and a fixed sixteen-row item list
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EQUIP-005, FND-RESEARCH-002, FND-RESEARCH-003, FND-RESEARCH-004, FND-EQUIP-009, FND-STATE-001, FND-AUDIO-011, FND-EXE-004, FND-PLATFORM-002]
conflicting: []
split_with: []
related: [RULE-RESEARCH-001, RULE-SITE-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05007` | None | `(104, 124, 344, 209)` | The panel is open | FND-EQUIP-005, FND-RESEARCH-003 |
| Gang portrait | `DATA/PX16/PX03000`, the 64-by-64 cell of the acting gang's definition | The acting gang | Panel `(26, 17, 64, 64)` | The panel is open | FND-RESEARCH-004 |
| Category frame | `DATA/PX16/PX00129` crop `(120,171,34,34)`, keyed on exact white | The chosen category | `(207, 139 + 36 * n, 34, 34)`, one pixel outside category cell `n` | Always; the panel opens on category 0, or on the category of the item in `target` when the gang's `action` is already Research | FND-EQUIP-009 |
| Item list, sixteen fixed rows | The plain font of `DATA/PX16/PX00129` (`fn_00413FD5`) on a black area, panel `(147, 25, 181, 144)` | For the chosen category, each item the player has not finished researching whose `tech_level` is within the gang type's Tech Level as capped by the sector's research-site level, in item order: its name and the player's `research_remaining` for it | Name at panel `(148, 26 + 9 * k)`, number at panel `(316, 26 + 9 * k)` for row `k` | The panel is open | FND-EQUIP-005, FND-RESEARCH-002, FND-RESEARCH-003 |

## Mouse input

Rectangles are in the shared panel's own coordinates; the panel is at
`(104,124)` on the screen [FND-RESEARCH-003].

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Category cell `n`, `n` 0 to 3 | Panel `(104, 16 + 36 * n, 32, 32)` | Always | Chooses category `n`, redraws the frame, rebuilds the list and clears the selection | FND-RESEARCH-003, FND-RESEARCH-004, FND-EQUIP-009 |
| Item list, press | Panel `(148, 26, 180, 143)` | Always | Selects the row `(y - 26) / 9`, in panel coordinates, and enables the confirmation control; a row with no entry clears the selection and disables it | FND-EQUIP-005, FND-RESEARCH-004 |
| Item list, double-click | Panel `(148, 19, 180, 143)` | The row holds an entry | Opens the Item Information panel for the row `(y - 26) / 9`, truncated toward zero, and selects nothing | FND-RESEARCH-004 |
| Gang portrait, double-click | Panel `(26, 17, 64, 64)` | Always | Opens the gang definition panel (SCR-GANG-001) for the acting gang | FND-RESEARCH-004 |
| Cancel control | Panel `(33, 137, 49, 22)` | Always | On release inside, closes the panel and leaves the gang's order as it was | FND-RESEARCH-004 |
| Confirmation control | Panel `(33, 169, 49, 22)` | A row is selected; otherwise a press plays the rejected sound | Writes the selected item to the gang's `target` and closes the panel; RULE-RESEARCH-001 carries the order out | FND-RESEARCH-004 |
| Outside the panel | Anywhere outside `(104, 124, 344, 209)` on the screen | Always | Plays the rejected sound | FND-RESEARCH-004 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Enter or Execute | The panel is open | With a row selected, as the confirmation control; otherwise plays the rejected sound | FND-RESEARCH-004 |
| Escape | The panel is open | As the Cancel control | FND-RESEARCH-004 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Rejected input | `DATA/SND00204` (effect slot 4) | A press outside the panel, or confirmation with no row selected | FND-RESEARCH-004, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Nothing selected | The Research command is chosen for a gang whose `action` is not Research, or a category is chosen, or a row with no entry is pressed | A row is selected, or the panel closes | FND-EQUIP-005, FND-RESEARCH-004 |
| Row selected | A row with an entry is pressed, or the panel opens for a gang whose `action` is already Research, with its `target` item's row selected | Another row or category is chosen, or the panel closes | FND-RESEARCH-004 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The double-click rectangle starts at panel y 19, seven pixels above the
  press rectangle, and the row number truncates toward zero, so a
  double-click up to seven pixels above the first row opens that row's item
  while a press there selects nothing.
- The list builder caps the Tech Level at 5 or 8 by the gang's sector's
  `research_level`, which a completed site whose special field is 1 or 2 sets
  (RULE-SITE-001). Whether those are the manual's Science Centers and Research
  Labs has not been checked against the site data.
- The colours of a row are not recorded. The Item Information panel
  (`DATA/PX16/PX05001`) belongs to another area; its screen ID should be
  named in the effects once it exists.
- When the game runs with the 8-bit image set it uses the `DATA/PX08` file of
  the same name (FND-PLATFORM-002).
