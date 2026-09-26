---
id: SCR-UI-005
title: Gangs in Sector panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-002, FND-UI-006, FND-UI-011, FND-UI-014, FND-UI-024, FND-AUDIO-011, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-UI-003, RULE-UI-004, RULE-UI-010, SCR-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel with its labels | `DATA/PX16/PX05009` | None | `(104,124,344,209)` once slid in | Always | FND-UI-002, FND-UI-011 |
| Sector tile | 54-by-52 cell of the drawn city map, with an unfilled black frame | The sector | `(135,135,54,52)` | Always | FND-UI-014 |
| Sector code | The font of `fn_00413FD5` | A column letter A to H and a row digit 1 to 8 | From `(156,190)` | Always | FND-UI-014 |
| Gang portrait | `DATA/PX16/PX03000` cell of the gang's definition, scaled to 32 by 32 | None | `(248 + 32*n, 138, 32, 32)` for column `n` | For each gang RULE-UI-010's `sector_roster_slots` lists | FND-UI-002 |
| Tech Level, Upkeep, Combat, Defense, Stealth, Detect | The digits of `DATA/PX16/PX00129` | The Tech Level and the negated Upkeep of the gang's definition in `DATA/Gangs`, then the gang's record offsets `0x12` to `0x15`, by `number_cells` with width 2 (RULE-UI-004); Upkeep shows in red | Two cells from x `258 + 32*n`, on the baselines 172, 181, 191, 200, 209 and 218 | For each column | FND-UI-002, FND-UI-006, FND-UI-014, FND-UI-024 |
| The ten Command Skills | The digits of `DATA/PX16/PX00129` | The gang's `chaos`, `control`, `heal`, `influence`, `research`, `strength`, `blade`, `ranged`, `fighting` and `martial_arts`, by `modifier_cells` with width 2 (RULE-UI-004) | Two cells from x `258 + 32*n`, on the baselines 228, 237, 246, 255, 264, 274, 283, 292, 301 and 310, read from record offsets `0x16` to `0x1F` in that order | For each column | FND-UI-002, FND-UI-006, FND-UI-014 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Close face | `(137,293,49,22)` | While the panel is open | Slides the panel out (RULE-UI-003) and returns to the screen it was opened from, when the button is released inside | FND-UI-011, FND-UI-014, FND-UI-024 |
| Inside the panel, off the face | The rest of `(104,124,344,209)` | While the panel is open | None | FND-UI-024 |
| Outside the panel | Outside `(104,124,344,209)` | While the panel is open | Refused with slot 4 | FND-UI-014, FND-UI-024 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| `Enter` or `Execute` (virtual key `0x2B`) | While the panel is open | Closes the panel; no other key does anything | FND-UI-014, FND-UI-024 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| None | | | | FND-UI-002 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Slide in and out | `DATA/SND00200`, `DATA/SND00201` | With Slide Panels on, as the panel opens and closes (RULE-UI-003) | FND-UI-011 |
| Refused | `DATA/SND00204` (slot 4) | When the panel refuses an input, and in place of the panel when the player has no gang in the sector | FND-AUDIO-011, FND-UI-014 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | The upper half of the Gangs/Hire tile (RULE-UI-002 route 7) on SCR-UI-003 or SCR-UI-004, when the sector record's byte at offset `0x10 + player` is nonzero; otherwise the tile only plays slot 4 | The player closes it | FND-UI-002, FND-UI-014 |

## Timing

The slide takes about a quarter of a second (RULE-UI-003).

## Differences between builds

None known.

## Open questions

- The screen positions follow from the handler's backing-buffer positions
  through the shared panel's mapping, screen = buffer + (104, -20)
  (FND-UI-014); they have not been checked against a capture.
- The Base Statistics option does not affect this panel: the handler never
  reads it (FND-UI-014). Whether the gang record's offsets `0x12` to `0x15`
  hold base or effective values is a question of FMT-STATE-001.
- A double click is handled as a single press. The handler does not limit the
  number of columns; a seventh of the player's gangs in one sector would draw
  from x 440, past the panel's right edge (FND-UI-024).
- Which code writes the sector bytes at offsets `0x10` to `0x15` has not been
  read.
