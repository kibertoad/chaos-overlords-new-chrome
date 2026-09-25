---
id: SCR-UI-008
title: Game Information panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-003, FND-UI-011, FND-UI-032, FND-AUDIO-011, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-UI-003, RULE-UI-009, SCR-UI-003]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel with its labels | `DATA/PX16/PX05021`, its left 320 pixels | None | `(128,124,320,209)` once slid in | Always | FND-UI-003, FND-UI-011 |
| Scenario | The font strip of `DATA/PX16/PX00129` | `game_info_scenario_text` (RULE-UI-009) | From `(228,151)` | Always | FND-UI-003 |
| Mentality | The font strip of `DATA/PX16/PX00129` | `game_info_mentality_text` (RULE-UI-009) | From `(228,169)` | Always | FND-UI-003 |
| Time limit | The font strip of `DATA/PX16/PX00129` | `game_info_limit_text` (RULE-UI-009) | From `(228,187)` | Always | FND-UI-003 |
| Player names | The font strip of `DATA/PX16/PX00129` | `player_names` of slot `n` | From `(240, 214 + 9*n)` | For each of the six slots | FND-UI-003 |
| Player status | The font strip of `DATA/PX16/PX00129` | `player_status_text(n)` (RULE-UI-009) | Right-aligned to x 408 on row `214 + 9*n` | For each of the six slots | FND-UI-003 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| OK face | `(161,293,49,22)` | While open | Closes the panel (RULE-UI-003) | FND-UI-003 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| None known | | | FND-UI-003 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| None | | | | FND-UI-003 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Refused | `DATA/SND00204` (slot 4) | A refused input | FND-AUDIO-011 |
| Slide in and out | `DATA/SND00200`, `DATA/SND00201` | With Slide Panels on (RULE-UI-003) | FND-UI-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Open | The Game Info tile (RULE-UI-002 route 12); by itself at the start of a multi-player game and when a saved game is opened | The OK face | FND-UI-032, SRC-MANUAL-GOG |

## Timing

The slide takes about a quarter of a second (RULE-UI-003).

## Differences between builds

None known.

## Open questions

- The keys the panel takes.
- The automatic opening at the start of a multi-player game and after loading
  rests on the manual; the code that does it has not been read.
- The player names' colours, which the manual says match the players.
