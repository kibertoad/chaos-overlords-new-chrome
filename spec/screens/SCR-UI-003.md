---
id: SCR-UI-003
title: City screen and main console
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-032, FND-UI-031, FND-UI-033, FND-UI-035, FND-UI-034, FND-TIMER-001, FND-AUDIO-010, FND-AUDIO-012, FND-AUDIO-001, FND-SEARCH-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-UI-001, RULE-UI-002, RULE-UI-006, RULE-UI-007, RULE-UI-011, RULE-UI-012, RULE-TIMER-002, RULE-TIMER-003, RULE-OPTIONS-003, RULE-AUDIO-001, RULE-AUDIO-007, RULE-AUDIO-008, SCR-UI-004, SCR-UI-005, SCR-UI-008, SCR-OPTIONS-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| City view and right control panel | `DATA/PX16/PX00128` | None | `(0,0,640,460)` | Always | FND-UI-032 |
| Neutral city map | `DATA/PX16/PX10000` | None | Map surface placed at `(2,44)`; the 8-by-8 grid starts at map `(4,3)` with 54-by-52 cells on a 53-by-51 stride | Always | FND-UI-033 |
| Owned sector interiors | `DATA/PX16/PX10001` to `DATA/PX16/PX10006`, one per player | The owner of each sector, `sectors[s].owner` | The 52-by-50 interior of each owned cell | For each sector whose owner is not -1 | FND-UI-033 |
| Objective pylons | `DATA/PX16/PX00129` crop `(344,15,54,52)`, keyed on exact white | Whether the sector is an objective sector (RULE-UI-012) | The whole cell, `(2 + 4 + 53*column, 44 + 3 + 51*row, 54, 52)` | Siege and Big Man, on each sector RULE-UI-012 marks | FND-UI-033 |
| Gang-status marker | `DATA/PX16/PX00129` crop `(492, 67 + 20*f, 20, 20)`, keyed on exact white | The frame `f` RULE-UI-006 picks for the sector | Inside the sector's cell; offset not recorded | When RULE-UI-006 returns a frame | FND-UI-031 |
| Site markers | `DATA/PX16/PX00150` | The sector's sites the player controls or selected in Search | Inside the sector's cell | See the Search panel | FND-SEARCH-001 |
| Overlord bar portraits | `DATA/PX16/PX00129` portraits at source y 480, 32 by 32, opaque | Each player's Overlord | `(16 + 72*n, 4, 32, 32)` for player `n` | Always | FND-UI-031 |
| Active-player marker | `DATA/PX16/PX00129` twelve 20-by-20 frames at source y 626, opaque | Which player is planning | `(48 + 72*n, 4, 20, 20)` | While player `n` plans | FND-UI-031 |
| Sector values | The font strip of `DATA/PX16/PX00129` | Income, Tolerance, Support and Cash of the selected sector (RULE-UI-011) | Rows of the console; positions not recorded | When a sector is selected | FND-UI-035 |
| Pressed console tiles | `DATA/PX16/PX00129` 48-by-48 cells at `(0,512)`, `(48,512)`, `(96,512)`, `(144,512)`, `(192,512)` and `(240,512)`; Done `(288,512,100,48)`; Game Info `(190,386,26,34)`; all opaque | None | Over the tile pressed | While a tile is held and the pointer is over it (RULE-UI-001) | FND-UI-032 |
| Planning clock bar | Not recorded | The remaining planning time, as the width RULE-TIMER-003 gives | 60 by 3 on the console; place not recorded | While a timed human player plans | FND-TIMER-001 |

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
| City sector | Each cell of the map | Planning | A click selects the sector; a double-click opens SCR-UI-004 | SRC-MANUAL-GOG |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Up, Down, Left, Right | Planning | Move the selected sector | SRC-MANUAL-GOG |
| Enter | Planning | Opens SCR-UI-004 for the selected sector | SRC-MANUAL-GOG |

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
- The places of the gang-status markers and site markers inside a cell, of the
  sector value rows, and of the clock bar are not recorded. The clock bar is
  believed to be at `(520,336,60,3)`, and the Overlord bar positions were
  measured from the art, not from the code.
- Which image set is drawn when Thousands of Colors is off (`DATA/PX08` rather
  than `DATA/PX16`) is not recorded here.
- The Hire dock at the bottom right belongs to the Hire screens; its reject
  gates are in FND-UI-032.
- The code that handles sector clicks and keys has not been read; those rows
  rest on the manual.
