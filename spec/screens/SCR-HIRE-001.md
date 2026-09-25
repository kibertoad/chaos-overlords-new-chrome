---
id: SCR-HIRE-001
title: Hire comparison panel showing the three offers side by side
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-HIRE-003, FND-UI-006, FND-PLATFORM-002]
conflicting: []
split_with: []
related: [RULE-HIRE-002]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05016` | None | Not recorded | The panel is open | FND-HIRE-003 |
| Baseline values, six per offer | Digit glyphs of `DATA/PX16/PX00129` | For the gang definition each entry of `hire_offers` names (RULE-HIRE-002): its Tech Level, Upkeep, Combat, Defense, Stealth and Detect, each a signed value two cells wide, a one-digit value in the right cell, a negative value in the red digit row without a minus sign, a zero in bright green | Not recorded | The panel is open | FND-HIRE-003, FND-UI-006 |
| Modifier values, ten per offer | Digit glyphs of `DATA/PX16/PX00129` | Ten further signed statistics of the same definitions, drawn the same way, a zero in dim green | Not recorded | The panel is open | FND-HIRE-003, FND-UI-006 |

## Mouse input

None known.

## Keyboard input

None known.

## Other input

None.

## Sounds

None known.

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | The player opens the panel from the main console | The player closes the panel | FND-HIRE-003 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The panel resource is named from its visible content, not from a finding;
  the call in `0x004546C5` that loads it has not been written down. When the
  game runs with the 8-bit image set it uses the `DATA/PX08` file of the same
  name (FND-PLATFORM-002).
- Unconfirmed leads for positions: the panel may use the alternate
  320-pixel-wide route drawn at `(128, 124)`, with three 32-by-32 gang
  portraits from `DATA/PX16/PX03000` starting at panel `(164, 14)` with a
  40-pixel pitch, value fields starting at panel x 174, 214 and 254, rows on
  the panel's own uneven 9- and 10-pixel baselines, and a close control at
  `(161, 293, 49, 22)`. None of these is backed by a finding yet.
- Which statistic each of the ten modifier rows shows, and in what order, is
  not recorded.
- The rule that turns a value into two cells is the UI area's number-drawing
  rule; its ID should be added to `related` once it exists.
- Whether this panel accepts any input other than closing, and how it is
  opened (the console route is from the UI findings), is not recorded here.
