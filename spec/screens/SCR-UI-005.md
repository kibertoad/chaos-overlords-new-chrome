---
id: SCR-UI-005
title: Gangs in Sector panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-002, FND-UI-006, FND-UI-011, FND-AUDIO-011, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-UI-003, RULE-UI-004, RULE-UI-010, SCR-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel with its labels | `DATA/PX16/PX05009` | None | `(104,124,344,209)` once slid in | Always | FND-UI-002, FND-UI-011 |
| Gang portrait | `DATA/PX16/PX03000` cell of the gang's definition, scaled to 32 by 32 | None | `(248 + 32*n, 138, 32, 32)` for column `n` | For each gang RULE-UI-010's `sector_roster_slots` lists | FND-UI-002 |
| Tech Level, Upkeep, Combat, Defense, Stealth, Detect | The digits of `DATA/PX16/PX00129` | The gang's values, by `number_cells` with width 2 (RULE-UI-004) | Two cells from x `258 + 32*n`, on the baselines 172, 181, 191, 200, 209 and 218 | For each column | FND-UI-002, FND-UI-006 |
| The ten Command Skills | The digits of `DATA/PX16/PX00129` | The gang's `chaos`, `control`, `heal`, `influence`, `research`, `strength`, `blade`, `ranged`, `fighting` and `martial_arts`, by `modifier_cells` with width 2 (RULE-UI-004) | Two cells from x `258 + 32*n`, on the baselines 228, 237, 246, 255, 264, 274, 283, 292, 301 and 310 | For each column | FND-UI-002, FND-UI-006 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Close face | Not recorded | While the panel is open | Slides the panel out (RULE-UI-003) and returns to the screen it was opened from | FND-UI-011 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| None known | | | FND-UI-002 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| None | | | | FND-UI-002 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Slide in and out | `DATA/SND00200`, `DATA/SND00201` | With Slide Panels on, as the panel opens and closes (RULE-UI-003) | FND-UI-011 |
| Refused | `DATA/SND00204` (slot 4) | When the panel refuses an input | FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | The upper half of the Gangs/Hire tile (RULE-UI-002 route 7) on SCR-UI-003 or SCR-UI-004 | The player closes it | FND-UI-002 |

## Timing

The slide takes about a quarter of a second (RULE-UI-003).

## Differences between builds

None known.

## Open questions

- The finding gives the portrait cell as `(144 + 32*n, 158)` in the panel's
  backing buffer, whose rows start at 144 and whose columns start at the panel's
  left edge; the screen position above follows from that and has not been
  checked against a capture.
- Which values the Base Statistics option switches to base values.
- The close face and the keys the panel takes have not been recorded.
- The statistic order of the ten Command Skills follows the panel's labels and
  FMT-STATE-001; the field each row reads has not been recorded.
