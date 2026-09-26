---
id: SCR-OBJECTIVE-001
title: Player Rankings panel with one vertical rail per player and portraits placed by score
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-OBJECTIVE-005, FND-OBJECTIVE-001, FND-AI-005, FND-UI-011, FND-UI-032, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-OBJECTIVE-002, RULE-UI-002, SCR-UI-003]
---

## Drawn elements

Positions inside the panel are panel-local, from its top-left corner. `hi` and
`lo` are the highest and lowest `scenario_score` of the active players, and
`offset` for slot `p` is `(hi - scenario_score[p]) * (140.0 / (hi - lo + 1))`
in single precision with its fraction removed toward zero, or 70 when `hi`
equals `lo`.

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel with six player-colour rails and a close button | `DATA/PX16/PX05011` | None | `(104, 124, 344, 209)` once it has slid in | While the panel is open | FND-OBJECTIVE-001, FND-OBJECTIVE-005, FND-UI-011 |
| Overlord portrait, per player slot `p` (0 to 5) | Interface sheet, source `(32 * portrait, 480, 32, 32)`, unscaled | How far the player's `scenario_score` is behind the leader's | Panel-local `(98 + 40 * p, 18 + offset, 32, 32)` | `scenario_standing[p]` is not `0xFF` | FND-OBJECTIVE-005 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Close button | `(137, 293, 50, 23)`; the press must land in panel-local `(33, 169, 49, 22)` | Always | Held-button press; closes on a release inside | FND-OBJECTIVE-005 |
| Outside the panel | Outside `(104, 124, 344, 209)` | Always | Plays the rejection sound, slot 4 | FND-OBJECTIVE-005 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Enter or `VK_EXECUTE` (`0x0D`, `0x2B`) | Always | Draws the close button pressed and closes | FND-OBJECTIVE-005 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Rejection (slot 4) | Not recorded here | A press outside the panel | FND-OBJECTIVE-005 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | The Ranking control of SCR-UI-003 is pressed during planning (RULE-UI-002, route 9) | The close button is released inside, or Enter or `VK_EXECUTE` is pressed | FND-UI-032, FND-OBJECTIVE-001, FND-OBJECTIVE-005 |

## Timing

The panel slides in from the right edge and out to it, as every panel does
[FND-UI-011].

## Differences between builds

None known.

## Open questions

- Tied players share a height, since they share a score. The leader's
  portrait is at the top of its rail.
- The panel image is used through the `DATA/PX08` file of the same name in
  256-colour mode.
