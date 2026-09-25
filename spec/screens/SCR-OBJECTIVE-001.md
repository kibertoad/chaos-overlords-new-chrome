---
id: SCR-OBJECTIVE-001
title: Player Rankings panel with one vertical rail per player and portraits placed by standing
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-OBJECTIVE-001, FND-AI-005, FND-UI-011, FND-UI-032, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-OBJECTIVE-002, RULE-UI-002, SCR-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel with six player-colour rails | `DATA/PX16/PX05011` | None | `(104, 124, 344, 209)` once it has slid in | While the panel is open | FND-OBJECTIVE-001, FND-UI-011 |
| Overlord portrait, per player slot `p` (0 to 5) | 32 by 32, source not recorded | `scenario_standing` of the player, computed by RULE-OBJECTIVE-002, as the portrait's height on its rail | Panel-local x `98 + 40 * p`; y set by the standing, higher for a better standing | `scenario_standing[p]` is not `0xFF` | FND-OBJECTIVE-001, FND-AI-005 |

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
| Open | The Ranking control of SCR-UI-003 is pressed during planning (RULE-UI-002, route 9) | Not recorded | FND-UI-032, FND-OBJECTIVE-001 |

## Timing

The panel slides in from the right edge and out to it, as every panel does
[FND-UI-011].

## Differences between builds

None known.

## Open questions

- The formula for a portrait's vertical position (origin and step per
  standing) is not recorded, nor whether the x values are left edges or
  centres.
- The source sheet and cell of the 32-by-32 portraits are not recorded.
- How the panel is closed, and whether it reacts to the pointer, is not
  recorded.
- Tied players share a height, since they share a standing.
- The panel image is used through the `DATA/PX08` file of the same name in
  256-colour mode.
