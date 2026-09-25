---
id: SCR-UI-003
title: City screen and main console
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-032, FND-UI-015, FND-UI-017, FND-UI-018, FND-UI-019, FND-UI-031, FND-UI-033, FND-UI-035, FND-UI-034, FND-TIMER-001, FND-AUDIO-010, FND-AUDIO-012, FND-AUDIO-001, FND-SEARCH-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-UI-001, RULE-UI-002, RULE-UI-006, RULE-UI-007, RULE-UI-011, RULE-UI-012, RULE-TIMER-002, RULE-TIMER-003, RULE-OPTIONS-003, RULE-AUDIO-001, RULE-AUDIO-007, RULE-AUDIO-008, SCR-UI-004, SCR-UI-005, SCR-UI-008, SCR-OPTIONS-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| City view and right control panel | `DATA/PX16/PX00128` | None | `(0,0,640,460)` | Always | FND-UI-032 |
| Neutral city map | `DATA/PX16/PX10000` | None | The map surface's `(0,0,432,416)` copied to `(2,42)`; the 8-by-8 grid starts at map `(4,3)`, screen `(6,45)`, with 54-by-52 cells on a 53-by-51 stride | Always | FND-UI-017, FND-UI-033 |
| Selected-sector frame | `DATA/PX16/PX00129` crop `(236 + 54f, 15, 54, 52)`, keyed on exact white | Which sector is selected (`0x004ABC80`) | Over the selected cell, `(6 + 53*column, 45 + 51*row)` | Always | FND-UI-017 |
| Grid labels | Tabs at `PX00129` `(276,448)`, `(276,461)`, `(299,448)` and `(312,448)`, keyed on exact white, with the 6-by-7 font | Column letters A to H and row numbers 1 to 8 | Letters at `(21 + 53*column, 42)` and `(21 + 53*column, 444)`; numbers at `(3, 59 + 51*row)` and `(421, 59 + 51*row)` | Always (the switch at `0x00487848` is never cleared) | FND-UI-017 |
| Owned sector interiors | `DATA/PX16/PX10001` to `DATA/PX16/PX10006`, one per player | The owner of each sector, `sectors[s].owner` | The 52-by-50 interior of each owned cell | For each sector whose owner is not -1 | FND-UI-033 |
| Objective pylons | `DATA/PX16/PX00129` crop `(344,15,54,52)`, keyed on exact white | Whether the sector is an objective sector (RULE-UI-012) | The whole cell, map `(4 + 53*column, 3 + 51*row, 54, 52)`, screen `(6 + 53*column, 45 + 51*row)` | Siege and Big Man, on each sector RULE-UI-012 marks | FND-UI-017, FND-UI-033 |
| Gang-status marker | `DATA/PX16/PX00129` crop `(492, 67 + 20*f, 20, 20)`, keyed on exact white | The frame `f` RULE-UI-006 picks for the sector | `(35 + 53*column, 61 + 51*row, 20, 20)` | When RULE-UI-006 returns a frame | FND-UI-017, FND-UI-031 |
| Site markers | `DATA/PX16/PX00150` | The sector's sites the player controls or selected in Search | Inside the sector's cell | See the Search panel | FND-SEARCH-001 |
| Overlord bar portraits | `DATA/PX16/PX00129` portraits at source y 480, 32 by 32, opaque | Each player's Overlord; the row at source y 594 for a player with no gang the active player can see in the sector, on SCR-UI-004 | `(18 + 70*n, 5, 32, 32)` for player `n`; an empty seat shows the 54-by-32 art at `(404,448)`, cycling through three frames 27 pixels apart | Always | FND-UI-017, FND-UI-031 |
| Active-player marker | `DATA/PX16/PX00129` twelve 20-by-20 frames at source y 626, opaque | The viewed player (`0x00487B8C`), which is the planning player on this screen | `(50 + 70*n, 6, 20, 20)`, frames stepped by the input pump | While player `n` is viewed | FND-UI-017, FND-UI-031 |
| Planning lights | `DATA/PX16/PX00129` `(66,347,20,6)`, or black | Whether a human seat has yet to complete its orders | `(51 + 70*n, 30, 20, 6)` | For each seat in play | FND-UI-017 |
| Sector values | The font strip of `DATA/PX16/PX00129` | Income, Tolerance, Support and Cash of the selected sector (RULE-UI-011) | Sector name, Income word, Tolerance, Support and Cash at x 568, y 60, 69, 78, 87 and 96; Support and Cash show 0 unless the active player owns the sector | When a sector is selected | FND-UI-017, FND-UI-035 |
| Pressed console tiles | `DATA/PX16/PX00129` 48-by-48 cells at `(0,512)`, `(48,512)`, `(96,512)`, `(144,512)`, `(192,512)` and `(240,512)`; Done `(288,512,100,48)`; Game Info `(190,386,26,34)`; all opaque | None | Over the tile pressed | While a tile is held and the pointer is over it (RULE-UI-001) | FND-UI-032 |
| Planning clock bar | `DATA/PX16/PX00129` green strip `(354,0,60,3)` | The remaining planning time, as the width RULE-TIMER-003 gives | `(520,336,60,3)`, drawn full when planning starts and ends | While a timed human player plans | FND-TIMER-001, FND-UI-017 |
| Hire dock | The offered gangs' 64-by-64 portraits; the snub cross `PX00129` `(178,299)` and the hire stamp `(114,299)`, keyed on exact white | The active player's three `hire_offers` and their `hire_orders` | `(440 + 66*k, 373, 64, 64)` for offer `k` | Always | FND-UI-017 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Events | `(500,126,48,48)` | Planning | RULE-UI-002 route 0: the Last Turn Events panel | FND-UI-032 |
| Comlink, View | `(552,126,48,33)` | Planning | RULE-UI-002 route 1: Comlink View | FND-UI-032 |
| Comlink, Send | `(552,159,48,15)` | Planning | RULE-UI-002 route 2: Comlink Send | FND-UI-032 |
| Combat, Results | `(500,178,48,33)` | Planning | RULE-UI-002 route 3: Combat Results | FND-UI-032 |
| Combat, Detailed | `(500,211,48,15)` | Planning | RULE-UI-002 route 4: Detailed Combat | FND-UI-032 |
| Finance, City | `(552,178,48,33)` | Planning | RULE-UI-002 route 5: City Financial | FND-UI-032 |
| Finance, Sector | `(552,211,48,15)` | Planning | RULE-UI-002 route 6: Sector Financial | FND-UI-032 |
| Gangs | `(500,230,48,33)` | Planning | RULE-UI-002 route 7: SCR-UI-005 | FND-UI-032 |
| Hire | `(500,263,48,15)` | Planning | RULE-UI-002 route 8: the Hire comparison panel | FND-UI-032 |
| Ranking | `(552,230,48,25)` | Planning | RULE-UI-002 route 9: Player Ranking | FND-UI-032 |
| Search | `(552,255,48,23)` | Planning | RULE-UI-002 route 10: Search: Sites | FND-UI-032 |
| Done | `(500,282,100,48)` | Planning | RULE-UI-002 route 11: RULE-OPTIONS-003, then the end of the player's planning | FND-UI-032 |
| Game Info | `(588,41,26,34)` | Planning | RULE-UI-002 route 12: SCR-UI-008 | FND-UI-032 |
| City sector, press | `(2,42,432,416)`, cell `(x - 2) / 54 + ((y - 42) / 52) * 8` | Planning | Selects the sector, unless its owner byte is -2 | FND-UI-015 |
| City sector, double-click | The same | Planning | Makes the sector the selected one and opens SCR-UI-004 for the active player's gangs | FND-UI-015 |
| Hire dock | `(440,373,196,77)` | Planning | The Hire handler, press and double-click (SCR-HIRE-001) | FND-UI-015, FND-UI-032 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Up, Down, Left, Right | Planning | Move the selected sector one cell; at the edge of the grid the selection stays | FND-UI-015 |
| Enter (virtual keys `0x0D` and `0x2B`) | Planning | Opens SCR-UI-004 for the selected sector | FND-UI-015 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| None | | | | FND-UI-032 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Press | `DATA/SND00202` (slot 2) | A console tile is pressed (RULE-UI-001) | FND-UI-032, FND-AUDIO-010 |
| Comlink alert | `DATA/Snd00205` (slot 6) | On entry with unread mail, and every 24 ticks while mail is unread (RULE-AUDIO-007, RULE-AUDIO-008) | FND-AUDIO-012 |
| Clock warnings | `DATA/Snd00206` and `DATA/Snd00207` (slots 7 and 8) | The last ten seconds and the last second of a timed turn (RULE-TIMER-003) | FND-TIMER-001 |
| Game music | CD audio tracks 3 to 8, repeated (RULE-AUDIO-001 mode 2) | From the start of the game | FND-AUDIO-001 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Planning | A human player's planning starts | Done is accepted, or the time limit passes (RULE-TIMER-002) | FND-UI-032, FND-TIMER-001 |
| City view | Planning starts, or SCR-UI-004 is left; the byte `0x00487B88` is 1 | SCR-UI-004 opens and clears it | FND-UI-015 |
| Waiting | After Done, while other players still plan; the same two views take input and the group strip of SCR-UI-004 is not drawn | The wait loop `fn_00471F06` ends | FND-UI-015, FND-UI-018 |
| Busy | Resolution, setup of a city or loading runs; the pointer is the hourglass (RULE-UI-007) | The work ends; the pointer is the arrow again | FND-UI-034 |

## Timing

The planning clock bar is redrawn on every sixth call of the input pump
(RULE-TIMER-003); how often that is in milliseconds is not recorded. The Comlink
alert repeats every 24 ticks of `presentation_tick`, about four seconds.

## Differences between builds

None known.

## Open questions

- The drawn area is 640 by 460 below the window's menu bar; how that maps onto
  the 640-by-480 canvas is not recorded.
- The offsets of the site markers inside a cell are not recorded here (see the
  Search panel).
- FND-UI-033 places the map at `(2,44)`; the copy in FND-UI-017 puts it at
  `(2,42)`, and the selection frame and the cell restore agree with `(2,42)`.
  The rows above use `(2,42)`.
- The hit grid of a city click (54 by 52 from `(2,42)`) drifts from the drawn
  grid (53 by 51 from `(6,45)`) by up to four pixels toward the bottom right
  (FND-UI-015). The rows above record both as found.
- Which words string resources 20 to 24 give the Income row is not recorded.
- Which image set is drawn when Thousands of Colors is off (`DATA/PX08` rather
  than `DATA/PX16`) is not recorded here.
- The Hire dock's input belongs to the Hire screens; its reject gates are in
  FND-UI-032.
