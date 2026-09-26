---
id: SCR-HIRE-001
title: Hire comparison panel showing the three offers side by side
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-HIRE-003, FND-HIRE-007, FND-HIRE-009, FND-UI-006, FND-AUDIO-011, FND-PLATFORM-002, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-HIRE-002]
---

## Drawn elements

Positions are screen coordinates while the panel is fully shown; `s` is the
offer slot, 0 to 2.

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05016` | None | `(128, 124)` to `(448, 333)`, slid in from the right | The panel is open | FND-HIRE-003, FND-HIRE-009 |
| Offer portraits | `DATA/PX16/PX03000` | The portrait of the gang definition each entry of `hire_offers` names (RULE-HIRE-002), scaled from 64 by 64 to 32 by 32 | `(292 + 40s, 138)`, 32 by 32 | The panel is open | FND-HIRE-009, FND-HIRE-007 |
| Baseline values, six per offer | Digit glyphs of `DATA/PX16/PX00129` | For the same definition, top to bottom: Tech Level; Upkeep as a negative number; Combat plus Strength plus Fighting plus Martial Arts; Defense; Stealth; Detect. Each is a signed value two cells wide, a one-digit value in the right cell, a negative value in the red digit row without a minus sign, a zero in bright green | x `302 + 40s`, y 172, 181, 191, 200, 209 and 218 | The panel is open | FND-HIRE-003, FND-HIRE-009, FND-UI-006 |
| Modifier values, ten per offer | Digit glyphs of `DATA/PX16/PX00129` | Chaos, Control, Heal, Influence, Research, Strength, Blade, Range, Fighting and Martial Arts of the same definition, top to bottom, drawn the same way, a zero in dim green | x `302 + 40s`, y 228, 237, 246, 255, 264, 274, 283, 292, 301 and 310 | The panel is open | FND-HIRE-003, FND-HIRE-009, FND-UI-006 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Close control | `(161, 293, 49, 22)`; a press counts inside `(161, 293)` to `(210, 315)` | Always | Left press or double-click presses the control; the panel closes when the button is released over it | FND-HIRE-009 |
| Rest of the panel | Inside `(104, 124, 344, 209)` | Always | Nothing | FND-HIRE-009 |
| Outside the panel | Outside `(104, 124, 344, 209)` | Always | Refused with slot 4 | FND-HIRE-009 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Enter or Execute | Always | Presses the close control and closes the panel | FND-HIRE-009 |
| Any other key, Escape included | Always | Nothing | FND-HIRE-009 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Rejected input | `DATA/SND00204` (effect slot 4) | A press outside the panel rectangle | FND-HIRE-009, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | The console dispatcher opens the panel | The player presses the close control, Enter or Execute | FND-HIRE-003, FND-HIRE-009 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- When the game runs with the 8-bit image set it uses the `DATA/PX08` files
  of the same names (FND-PLATFORM-002).
- Whether the sum in the third row is meant as the gang's hand-to-hand
  strength or is a slip is not settled by the code (FND-HIRE-009).
- The rule that turns a value into two cells is the UI area's number-drawing
  rule; its ID should be added to `related` once it exists.
