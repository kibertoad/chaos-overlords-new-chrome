---
id: SCR-UI-004
title: Detailed sector screen
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-036, FND-UI-031, FND-UI-032, FND-UI-035, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-UI-002, RULE-UI-005, RULE-UI-006, RULE-UI-010, RULE-UI-011, SCR-UI-003, SCR-UI-007]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Console, Overlord bar and pressed tiles | As on SCR-UI-003 | As on SCR-UI-003 | As on SCR-UI-003 | Always | FND-UI-032 |
| 9 Sector Display | `DATA/PX16/PX10000` to `DATA/PX16/PX10006` | The selected sector at the centre of its 3-by-3 neighbourhood, with ownership | Starting at `(61,48)`; 54-by-52 cells | Always | SRC-MANUAL-GOG |
| Gang-status markers | `DATA/PX16/PX00129` crop `(492, 67 + 20*f, 20, 20)`, keyed on exact white | The frame RULE-UI-006 picks for each visible sector | Inside each cell; offset not recorded | When RULE-UI-006 returns a frame | FND-UI-031 |
| Site portraits | `DATA/PX16/PX02000`, the 120-by-64 row of the site's definition | The sector's three sites | Stacked from `(83,226)` | Always | SRC-MANUAL-GOG |
| Site progress meter | `DATA/PX16/PX00129` green strip `(354,0,100,3)` over the red track of the site frame | `site_meter_length(progress, resistance)` pixels (RULE-UI-005) | 100 by 3 at each site; place not recorded | For each site, when the sector's owner is the active player | FND-UI-036 |
| Gang card frame | `DATA/PX16/PX00129` `(162,15,74,110)` | None | `(254 + 76*(n % 2), 80 + 112*(n / 2), 74, 110)` for card `n` | For each gang RULE-UI-010 lists, at most six | FND-UI-036 |
| Action strip | `DATA/PX16/PX00129` `(162, 125 + 9*action, 64, 9)` | The gang's `action` | On the card; offset not recorded | For each card | FND-UI-036 |
| Gang portrait | `DATA/PX16/PX03000` 64-by-64 cell of the gang's definition | None | Card offset `(5,20)` | For each card | FND-UI-036 |
| Equipment icons | `DATA/PX16/PX04999` 20-by-20 cells | The gang's `weapon`, `armor` and `misc` | Card offsets `(5,86)`, `(27,86)`, `(49,86)` | For each item that is not -1 | FND-UI-036 |
| Force meter | `DATA/PX16/PX00129` green strip `(354,0)` over the card's 60-by-3 red track | `force_meter_length(force)` pixels (RULE-UI-005) | On the card; offset not recorded | For each card | FND-UI-036 |
| Player outline | A line in the player's colour | None | One pixel outside each card | For each card | FND-UI-036 |
| Sector values | The font strip of `DATA/PX16/PX00129` | Income, Tolerance, Support and Cash (RULE-UI-011) | Console rows; positions not recorded | Always | FND-UI-035 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Console tiles | As on SCR-UI-003 | Planning | RULE-UI-002 | FND-UI-032 |
| Neighbouring sector | Each outer cell of the 9 Sector Display | Planning | Makes that sector the selected one and redraws the screen around it | SRC-MANUAL-GOG |
| Site portrait | Each portrait | Planning | A double-click opens SCR-UI-007 for the site | SRC-MANUAL-GOG |
| Gang card | Each card | Planning | Selects the gang and opens its command box; a double-click opens the gang's information panel | SRC-MANUAL-GOG |
| Back to the city | Not recorded | Planning | Returns to SCR-UI-003 | SRC-MANUAL-GOG |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Up, Down, Left, Right | Planning | Move the selected sector, as clicking a neighbour does | SRC-MANUAL-GOG |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| None | | | | FND-UI-036 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| As on SCR-UI-003 | | | FND-UI-032 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Sector view | A double-click on a city sector, or Enter, on SCR-UI-003 | The player returns to the city, or planning ends | SRC-MANUAL-GOG |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The positions of the 9 Sector Display and the site portraits, and the offsets
  of the action strip, the Force meter and the markers, were measured from the
  art and from captures, not from the code.
- The control that returns to the city, and the code that handles clicks on
  neighbours, sites and cards, have not been read; those rows rest on the manual.
- Whether the site progress meter is drawn for a player who does not own the
  sector (the reading is that it is not).
