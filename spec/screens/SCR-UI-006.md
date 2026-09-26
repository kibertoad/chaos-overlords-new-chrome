---
id: SCR-UI-006
title: Item Information panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-004, FND-UI-006, FND-UI-011, FND-UI-013, FND-UI-024, FND-UI-023, FND-AUDIO-011, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-UI-003, RULE-UI-004]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel with its labels | `DATA/PX16/PX05001`, its left 320 pixels | None | `(128,124,320,209)` once slid in | Always | FND-UI-004, FND-UI-011 |
| Rotating item | The item's `DATA/PX16/PX04xxx` strip of fifteen 48-by-48 frames | The item's picture, frame 0 to 14 in turn, wrapping | `(162,141,48,48)` | Always | FND-UI-004 |
| Item name | The font strip of `DATA/PX16/PX00129` | The name in the item's `DATA/ITEMS` record | From `(228,151)` | Always | FND-UI-004 |
| Item type | The font strip of `DATA/PX16/PX00129` | The item's type, string-table entry 25 plus the record's `type` [FND-UI-013] | Right-aligned to x 408 on the name's row | Always | FND-UI-004 |
| Description | The font strip of `DATA/PX16/PX00129` | The three 30-character description rows of the item's record, as stored | From x 228 on rows 169, 178 and 187 | Always | FND-UI-004 |
| Cost and Tech Level | The digits of `DATA/PX16/PX00129` | The item's cost and tech level, by `number_cells` with width 2 (RULE-UI-004) | Two cells from x 300 and from x 396, screen row 216 (backing row 236) | Always | FND-UI-004, FND-UI-006, FND-UI-013 |
| Edge of the panel underneath | The left 24 columns of the panel the item was opened from | None | `(104,124,24,209)` | When opened from another panel, on a repaint | FND-UI-013 |
| The fourteen effects | The digits of `DATA/PX16/PX00129` | The item's fourteen statistic modifiers, by `modifier_cells` with width 2 (RULE-UI-004) | Two cells from x 300 and from x 396 on rows 243, 252, 270, 279, 288, 297 and 306 | Always | FND-UI-004, FND-UI-006 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Exit face | `(161,293,49,22)` | While open | Closes the panel (RULE-UI-003) | FND-UI-004 |
| Inside the panel, off the face | The rest of `(128,124,320,209)` | While open | None | FND-UI-013 |
| Outside the panel | Outside `(128,124,320,209)` | While open | Refused; plays slot 4 | FND-UI-004, FND-AUDIO-011, FND-UI-013 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Enter | While open | Closes the panel | FND-UI-004 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Keyboard | `VK_EXECUTE` | While open | Closes the panel | FND-UI-004 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted | `DATA/SND00203` (slot 3) | The exit face or Enter is accepted | FND-AUDIO-011 |
| Refused | `DATA/SND00204` (slot 4) | A press outside the panel | FND-AUDIO-011, FND-UI-013 |
| Slide in and out | `DATA/SND00200`, `DATA/SND00201` | With Slide Panels on (RULE-UI-003) | FND-UI-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | A double-click on an item in the Equip, Research, Attack, Give or Sell panels, the gang information panel, or a gang card of the sector screen. In Equip the list rows are panel-local x 148 to 328 from y 26, 9 pixels each, to y 169; in Research from y 19 to 162 | The exit face or Enter; a panel it was opened over stays on screen | FND-UI-004, FND-UI-013, FND-UI-024, SRC-MANUAL-GOG |

## Timing

The item turns one frame per `presentation_tick` it takes, at most six frames a
second, so a full turn of fifteen frames takes 2.5 seconds (RULE-UI-008). The
slide takes about a quarter of a second (RULE-UI-003).

## Differences between builds

None known.

## Open questions

- Which item each caller passes on a double-click is recorded with each
  caller; FND-UI-013 lists the eight functions that open the panel.
- `VK_EXECUTE` is a key code that no key on a US keyboard sends.
