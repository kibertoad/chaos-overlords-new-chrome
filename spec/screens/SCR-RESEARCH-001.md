---
id: SCR-RESEARCH-001
title: Research panel with item categories and a fixed sixteen-row item list
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-EQUIP-005, FND-RESEARCH-002, FND-PLATFORM-002]
conflicting: []
split_with: []
related: [RULE-RESEARCH-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05007` | None | Not recorded | The panel is open | FND-EQUIP-005 |
| Item list, sixteen fixed rows | Not recorded | The entries of the shared sixteen-entry list the list builder fills for the chosen category, each with the player's `research_remaining` for the item | Rows nine pixels apart, the first on the panel's baseline y 26 | The panel is open | FND-EQUIP-005, FND-RESEARCH-002 |

## Mouse input

Rectangles are in the shared panel's own coordinates; the panel's position on
the screen is an open question.

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
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
- The category controls: the Equip panel uses four 32-by-32 cells at panel
  `(104, 16)`, `(104, 52)`, `(104, 88)` and `(104, 124)`, and choosing one
  clears the choice and rebuilds the list. Whether the Research handler
  `0x004427FA` uses the same cells is not recorded.
- Which items the Research list builder admits (the manual's limits by the
  gang's Tech Level and by controlled Science Centers or Research Labs, and
  leaving out items already researched) is not recorded.
- The font, colours and columns of a row, the confirmation and Cancel
  controls, keyboard input, and the double-click that may open the Item
  Information panel (`DATA/PX16/PX05001`) are not recorded.
- The panel's origin on the screen is not recorded in a finding.
- When the game runs with the 8-bit image set it uses the `DATA/PX08` file of
  the same name (FND-PLATFORM-002).
