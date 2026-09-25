---
id: SCR-RESEARCH-001
title: Research panel with item categories and a fixed sixteen-row item list
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EQUIP-005, FND-RESEARCH-002, FND-RESEARCH-003, FND-EQUIP-009, FND-PLATFORM-002]
conflicting: []
split_with: []
related: [RULE-RESEARCH-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05007` | None | `(104, 124, 344, 209)` | The panel is open | FND-EQUIP-005, FND-RESEARCH-003 |
| Category frame | `DATA/PX16/PX00129` crop `(120,171,34,34)`, keyed on exact white | The chosen category | `(207, 139 + 36 * n, 34, 34)`, one pixel outside category cell `n` | Always; the panel opens on category 0, or on the category of the item in `target` when the gang's `action` is already Research | FND-EQUIP-009 |
| Item list, sixteen fixed rows | The plain font of `DATA/PX16/PX00129` (`fn_00413FD5`) on a black area, panel `(147, 25, 181, 144)` | For the chosen category, each item the player has not finished researching whose `tech_level` is within the gang type's Tech Level as capped by the sector's research-site level, in item order: its name and the player's `research_remaining` for it | Name at panel `(148, 26 + 9 * k)`, number at panel `(316, 26 + 9 * k)` for row `k` | The panel is open | FND-EQUIP-005, FND-RESEARCH-002, FND-RESEARCH-003 |

## Mouse input

Rectangles are in the shared panel's own coordinates; the panel is at
`(104,124)` on the screen [FND-RESEARCH-003].

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Category cell `n`, `n` 0 to 3 | Panel `(104, 16 + 36 * n, 32, 32)` | Always | Chooses category `n`, redraws the frame and rebuilds the list | FND-RESEARCH-003, FND-EQUIP-009 |
| Item list | Panel `(148, 19, 180, 143)` | The row holds an entry | Selects the row `(y - 26) / 9`, counted in panel coordinates and truncated toward zero, as the item to research; the Research order is carried out by RULE-RESEARCH-001 | FND-EQUIP-005 |

## Keyboard input

None known.

## Other input

None.

## Sounds

None known.

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | The Research command is chosen for a gang | The panel is closed | FND-EQUIP-005 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- Because the row number truncates toward zero, the first row takes panel y
  19 to 34, sixteen pixels, and rows 1 to 15 take nine pixels each, ending at
  y 161. The Equip panel's list rectangle is `(148, 26, 180, 143)` with the
  same rows, so the two lists line up but the Research list's first row
  reaches seven pixels higher.
- The list builder caps the Tech Level at 5 or 8 by byte `0x0D` of the
  gang's sector (FND-RESEARCH-003); which sites set that byte to 1 or 2, and
  whether that matches the manual's Science Centers and Research Labs, is
  not recorded here.
- The colours of a row, the confirmation and Cancel
  controls, keyboard input, and the double-click that may open the Item
  Information panel (`DATA/PX16/PX05001`) are not recorded.
- When the game runs with the 8-bit image set it uses the `DATA/PX08` file of
  the same name (FND-PLATFORM-002).
