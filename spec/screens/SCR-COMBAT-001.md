---
id: SCR-COMBAT-001
title: Combat Results panel, paged by sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-AUDIO-002, FND-AUDIO-011, FND-COMBAT-002, FND-COMBAT-004, FND-COMBAT-007, FND-COMBAT-009]
conflicting: []
split_with: []
related: [RULE-COMBAT-002]
---

## Drawn elements

The panel's origin is `(104,124)` [FND-COMBAT-002].

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05012` | None | Origin `(104, 124)`; size not recorded | While the panel is open | FND-COMBAT-002 |
| Viewer's forces | Not recorded | The viewer's row of `combat_results` for the page's sector, filled by RULE-COMBAT-002: up to six gangs, 40 by 40 each, in a grid of two columns 44 pixels apart and three rows 52 pixels apart | Grid origin `(207, 153)` | While the page's sector is shown | FND-AUDIO-011 |
| Enemy forces | Not recorded | The selected opponent's row of `combat_results` for the sector, laid out like the viewer's | Grid origin `(350, 153)` | While an opponent is selected | FND-AUDIO-011 |
| Opponent portraits | `DATA/PX16/PX00129` 32-by-32 portrait `(32 * portrait, 480)`, or `(32 * portrait, 594)` for a player with no result in the sector | The other five players in player order; a player with no result in the sector is drawn dim | `(306, 140 + 36 * n, 32, 32)`, `n` 0 to 4 | While the panel is open | FND-AUDIO-011, FND-COMBAT-002, FND-COMBAT-007 |
| Opponent frame | `DATA/PX16/PX00129` crop `(120,171,34,34)`, keyed on exact white | The chosen opponent | `(305, 139 + 36 * n, 34, 34)`, one pixel outside portrait `n` | While an opponent is chosen | FND-COMBAT-009 |
| Page counter | Two-cell numbers with leading zeros | The page number, from 1, and the page count | From `(138, 137)` and `(174, 137)` | While the panel is open | FND-COMBAT-007 |
| Arrows | `DATA/PX16/PX00129` 26-by-23 art: Previous from `(170, 363)` on the first page and `(118, 363)` otherwise; Next from `(196, 363)` on the last page and `(144, 363)` otherwise | Whether a page step is possible | `(135, 157)` and `(163, 157)` | While the panel is open | FND-COMBAT-007 |
| Sector tile | 54-by-52 cell of the drawn city map, framed in black | The page's sector | `(135, 191, 54, 52)` | While the panel is open | FND-COMBAT-007 |
| Police strip | `DATA/PX16/PX00129` `(0, 432, 54, 9)` | A police result in the sector | `(135, 191, 54, 9)` | When any player's police flag for the sector is set | FND-COMBAT-007 |
| Sector code | The font of `fn_00413FD5` | A column letter A to H and a row digit 1 to 8 | From `(156, 246)` | While the panel is open | FND-COMBAT-007 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Previous page | `(135, 157, 26, 23)` | Always; on the first page it only plays the rejected sound | Shows the previous qualifying sector | FND-AUDIO-011, FND-COMBAT-002 |
| Next page | `(163, 157, 26, 23)` | Always; on the last page it only plays the rejected sound | Shows the next qualifying sector | FND-AUDIO-011, FND-COMBAT-002 |
| Opponent portrait `n` | `(306, 140 + 36 * n, 32, 32)`, `n` 0 to 4 | The player has a result row in the sector | Selects that opponent's forces | FND-AUDIO-011, FND-COMBAT-002 |
| Viewer's force selector | `(205, 151, 88, 156)` | While a sector is shown | Selects one of the six result slots: right column when x is greater than 248, second row when y is greater than 202, third row when y is greater than 254 | FND-COMBAT-002 |
| Exit | `(137, 293, 49, 22)` | Always | Closes the panel | FND-COMBAT-002 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Not recorded | Always | The handler pages with the keyboard under the same rule as the arrows | FND-AUDIO-011 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` (effect slot 3) | A page step that changes the page, or a click that changes the selected opponent | FND-AUDIO-011 |
| Rejected input | `DATA/SND00204` (effect slot 4) | Previous on the first page or Next on the last page | FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Page `k` | The panel opens (page 0) or a page step reaches it. The pages are the qualifying sectors in ascending sector order: a sector qualifies when the viewer has a result there or has a gang there, and at least one player has a result there | A page step or Exit; paging does not wrap | FND-AUDIO-011 |
| Opponent selected | A page opens (the first opponent with a result) or an enabled portrait is clicked | Another enabled portrait is clicked, or the page changes | FND-AUDIO-011 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The page counter, sector tile, sector code, police strip and arrows are
  taken from the page renderer `fn_00453087` (FND-COMBAT-007); which rows of
  the interface sheet hold the greyed arrows and the dimmed portraits rests on
  its branch structure, not on a comparison of the art.
- What selecting a slot with the force selector changes on the panel.
- The keys that page, and whether any key closes the panel.
- The resources of the portraits in the grids and the opponent strip.
- The panel has no control that opens Detailed Combat (SCR-COMBAT-002), which
  the main console opens by its own route [FND-COMBAT-002].
- The panel exists as `DATA/PX08/PX05012` too; which file is drawn depends on
  the display mode.
