---
id: SCR-UI-007
title: Site Information panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-005, FND-UI-006, FND-UI-011, FND-AUDIO-011, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-UI-003, RULE-UI-004, SCR-UI-004]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel with its labels | `DATA/PX16/PX05002`, its left 320 pixels | None | `(128,124,320,209)` once slid in | Always | FND-UI-005, FND-UI-011 |
| Site portrait | `DATA/PX16/PX02000`, the 120-by-64 row of the site's definition | None | `(156,139,120,64)` | Always | FND-UI-005 |
| Site name | The font strip of `DATA/PX16/PX00129` | The name in the site's `DATA/SITES` record | From `(288,151)` | Always | FND-UI-005 |
| Resistance, Tolerance, Support, Cash | The digits of `DATA/PX16/PX00129` | The site's values, by `modifier_cells` with width 2 (RULE-UI-004) | Two cells from x 396 on rows 169, 187, 196 and 205 | Always | FND-UI-005, FND-UI-006 |
| The fourteen statistics | The digits of `DATA/PX16/PX00129` | The site's fourteen statistic modifiers, by `modifier_cells` with width 2 | Two cells from x 300 and from x 396 on rows 244, 253, 271, 280, 289, 298 and 307 | Always | FND-UI-005, FND-UI-006 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Close face | `(161,293,49,22)` | While open | Closes the panel (RULE-UI-003) and returns to the screen it was opened from | FND-UI-005 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| None known | | | FND-UI-005 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| None | | | | FND-UI-005 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Refused | `DATA/SND00204` (slot 4) | A refused input | FND-AUDIO-011 |
| Slide in and out | `DATA/SND00200`, `DATA/SND00201` | With Slide Panels on (RULE-UI-003) | FND-UI-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | A double-click on a site on SCR-UI-004, and other site lists | The close face | FND-UI-005, SRC-MANUAL-GOG |

## Timing

The slide takes about a quarter of a second (RULE-UI-003).

## Differences between builds

None known.

## Open questions

- Whether Resistance shows the site's remaining Resistance in the sector or its
  definition's base value.
- The keys the panel takes, which are likely those of Item Information.
