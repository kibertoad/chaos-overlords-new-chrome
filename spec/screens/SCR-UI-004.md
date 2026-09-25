---
id: SCR-UI-004
title: Detailed sector screen
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-036, FND-UI-015, FND-UI-017, FND-UI-018, FND-UI-019, FND-UI-025, FND-UI-031, FND-UI-032, FND-UI-035, SRC-MANUAL-GOG, FND-UI-021]
conflicting: []
split_with: []
related: [RULE-UI-002, RULE-UI-005, RULE-UI-006, RULE-UI-010, RULE-UI-011, SCR-UI-003, SCR-UI-007, SCR-HIRE-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Console, Overlord bar and pressed tiles | As on SCR-UI-003 | As on SCR-UI-003; a portrait is drawn from the row at source y 594 when that player has no gang here the active player can see (`gangs_seen`), and the animated marker follows the viewed player | As on SCR-UI-003 | Always | FND-UI-017, FND-UI-032 |
| Background | The 52-by-50 interior of the sector's cell in the unmarked copy of `DATA/PX16/PX10000` kept below y 416 of the city map surface, stretched and darkened through a one-bit pattern | The selected sector's terrain, without ownership colour or markers | `(2,42,432,416)` | Always | FND-UI-018, FND-UI-025 |
| Owner strip and back control | `DATA/PX16/PX00129` `(236 + 32*o, 67, 32, 207)` for owner `o + 1` (0 for none) and `(460,67,32,207)` | The sector's owner | `(4,43,32,207)` and `(4,250,32,207)` | Always | FND-UI-018 |
| 9 Sector Display | The prepared city map (`DATA/PX16/PX10000` to `DATA/PX16/PX10006`), the frame `DATA/PX16/PX00129` `(0,15,162,156)` keyed on exact white, and the grid labels | The selected sector at the centre of its 3-by-3 neighbourhood, with ownership and markers; cells off the map are black | `(64,60,162,156)`; cell `(i, j)` at `(65 + 53*i, 61 + 51*j)` | Always | FND-UI-018 |
| Gang-status markers | `DATA/PX16/PX00129` crop `(492, 67 + 20*f, 20, 20)`, keyed on exact white | The frame RULE-UI-006 picks for each visible sector | At the same offset in each cell as on SCR-UI-003, since the display is copied from the prepared map | When RULE-UI-006 returns a frame | FND-UI-017, FND-UI-031 |
| Site portraits | `DATA/PX16/PX02000`, the 120-by-64 row of the site's definition | The sector's three sites, under the frame `DATA/PX16/PX00129` `(0,171,120,64)` keyed on exact white | `(86, 228 + 66*k, 120, 64)` for site slot `k` | Always | FND-UI-018 |
| Site progress meter | `DATA/PX16/PX00129` green strip `(354,0,100,3)` over the red track of the site frame | `site_meter_length(progress, resistance)` pixels (RULE-UI-005) | Portrait offset `(10,59)`, 100 by 3 | For each site, when the sector's owner is the active player | FND-UI-018, FND-UI-036 |
| Gang card frame | `DATA/PX16/PX00129` `(162,15,74,110)` | None | `(254 + 76*(n % 2), 80 + 112*(n / 2), 74, 110)` for card `n` | For each gang RULE-UI-010 lists | FND-UI-036 |
| Action strip | `DATA/PX16/PX00129` `(162, 125 + 9*action, 64, 9)` | The gang's `action` | On the card; offset not recorded | For each card | FND-UI-036 |
| Gang portrait | `DATA/PX16/PX03000` 64-by-64 cell of the gang's definition | None | Card offset `(5,20)` | For each card | FND-UI-036 |
| Equipment icons | `DATA/PX16/PX04999` 20-by-20 cells | The gang's `weapon`, `armor` and `misc` | Card offsets `(5,86)`, `(27,86)`, `(49,86)` | For each item that is not -1 | FND-UI-036 |
| Force meter | `DATA/PX16/PX00129` green strip `(354,0)` over the card's 60-by-3 red track | `force_meter_length(force)` pixels (RULE-UI-005) | On the card; offset not recorded | For each card | FND-UI-036 |
| Player outline | A line in the viewed player's colour | None | One pixel outside each card | For each card | FND-UI-018, FND-UI-036 |
| Group order strip | `DATA/PX16/PX00129` `(190,425,152,16)` | None | `(253,61,152,16)` | At least two cards, the viewed player is the active player, and planning is not over | FND-UI-018 |
| Sector values | The font strip of `DATA/PX16/PX00129` | Income, Tolerance, Support and Cash (RULE-UI-011) | As on SCR-UI-003 | Always | FND-UI-017, FND-UI-035 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Console tiles | As on SCR-UI-003 | Planning | RULE-UI-002 | FND-UI-032 |
| Neighbouring sector | `(64,60,162,156)`; column `(x - 64 > 53) + (x - 64 > 107)`, row `(y - 60 > 51) + (y - 60 > 103)` | A double-click, on a cell that is on the map and whose owner byte is not -2 | Makes that sector the selected one and redraws the screen for the active player | FND-UI-015 |
| Site portrait | `(86,228,120,196)`; slot `(y > 294) + (y > 360)` | A double-click | Opens SCR-UI-007 for the site | FND-UI-015 |
| Gang card | `(254,80,150,336)`; card `(x > 329) + 2*(y > 192) + 2*(y > 304)` | A press (left or right) or a double-click, on a card that holds a gang | For the active player's own cards, the individual command handler with mode 1 for a press and 2 for a double-click; for another player's card, a double-click on the portrait opens the gang panel and on an equipment icon opens Item Information. On the active player's card, a press inside the card's strip from `(5,8)` to `(69,17)` opens a Windows popup menu at the card's corner moved 1 left and 8 down: menu 2 (recurring orders: Chaos, Control, Heal, Hide, Influence, Research, None) right of card x 37, menu 1 (the thirteen orders, None and Terminate) otherwise, with items that cannot apply greyed | FND-UI-015, FND-UI-021 |
| Group order strip | `(253,61,152,16)`; left half to x 367, right half beyond | A press, while the strip is drawn | Opens a Windows popup menu at `(290,65)`: menu 3 (Attack, Bribe, Chaos, Control, Heal, Hide, Influence, Move, Snitch, None, Terminate) on the left half, menu 5 (recurring Chaos, Control, Heal, Hide, Influence, None) on the right. The choice becomes the order of all the active player's gangs in the sector, for this turn (left) or recurring (right); Heal changes only gangs below Force 10 | FND-UI-015, FND-UI-021 |
| Overlord bar portrait | `(12 + 70*n, 5, 62, 32)` for player `n` | A press, when that player's `gangs_seen` byte for the sector is set | Shows player `n`'s gangs in the sector that the active player can see | FND-UI-015 |
| Hire dock | `(440,373,196,77)` | A press or double-click | The Hire handler (SCR-HIRE-001) | FND-UI-015 |
| Back to the city | `(4,394,32,63)` | A press (left or right), released inside | Returns to SCR-UI-003 with the same sector selected | FND-UI-015 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Up, Down, Left, Right | Planning | Move the selected sector one cell unless it is at that edge of the grid, and redraw for the active player | FND-UI-015 |
| Enter (virtual keys `0x0D` and `0x2B`) | Planning | Shows the back control pressed and returns to SCR-UI-003 | FND-UI-015 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| None | | | | FND-UI-015 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| As on SCR-UI-003 | | | FND-UI-032 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Sector view | A double-click on a city sector, or Enter, on SCR-UI-003; the byte `0x00487B88` is 0 | The back control or Enter, or planning ends | FND-UI-015 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The offsets of the action strip and the Force meter on a card were measured
  from the art, not from the code (the card compositor is FND-UI-036's).
- FND-UI-036 reads every caller of the compositor as passing the active
  player. The Overlord bar buttons pass another player (FND-UI-015), so the
  cards can show that player's gangs; the rows above follow FND-UI-015.
- Whether the site progress meter is drawn for a player who does not own the
  sector (the reading is that it is not).
