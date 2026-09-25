---
id: SCR-AWARDS-001
title: Endgame screen listing the players by place with their awards or their statistics
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-AWARDS-004, FND-AWARDS-003, FND-AWARDS-001, FND-GFX-003, FND-AUDIO-002, FND-AUDIO-010, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-AWARDS-001, RULE-AWARDS-002]
---

## Drawn elements

`row` is the row's position in `endgame_rows` (RULE-AWARDS-002), counted from
0. A ranked row belongs to an active player; an eliminated row to a player
whose standing is 0xFF.

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| City screen, left as it was | None | None | Whole screen | Always | FND-AWARDS-003 |
| Endgame frame with the Awards, Stats and Done controls | `DATA/PX16/PX00200` | None | `(106, 25, 428, 410)` | Always | FND-AWARDS-003, FND-AWARDS-004, FND-GFX-003 |
| Tab mark | Interface sheet, source `(488, 512, 8, 16)` | The selected tab | `(468, 33, 8, 16)` for Awards, `(520, 33, 8, 16)` for Stats | Always | FND-AWARDS-004 |
| Colour fill, per row | None | The row player's colour | `(111, 31 + 66 * row, 20, 62)` | Always | FND-AWARDS-004 |
| Place marker, per row | `DATA/PX16/PX00201`, source `(16 * standing, 48 + 32 * player, 16, 32)` | The row player's place, `scenario_standing + 1` | `(113, 31 + 66 * row, 16, 32)` | Ranked rows | FND-AWARDS-003, FND-AWARDS-004 |
| Portrait, per row | Interface sheet, source `(32 * portrait, 480, 32, 32)` | `portrait` of the row's player | `(132, 30 + 66 * row, 64, 64)`, scaled | Always | FND-AWARDS-004 |
| Player name, per row | Font not recorded | `player_names` of the row's player, unchanged and with no place prefix | From `(197, 38 + 66 * row)` | Always | FND-AWARDS-003, FND-AWARDS-004 |
| Score caption, per row | String at `0x00487704` | None | `(227, 62 + 66 * row)` | Ranked rows | FND-AWARDS-004 |
| Score, per row | Font not recorded | `scenario_score` of the row's player, 5 digits | `(227, 70 + 66 * row)` | Ranked rows | FND-AWARDS-004 |
| Awards strip, per row | `DATA/PX16/PX00201`, source `(96, 48, 160, 64)` | None | `(262, 30 + 66 * row, 160, 64)` | The Awards tab is selected | FND-AWARDS-004 |
| Award icons, per row | `DATA/PX16/PX00201`, source `(48 * code, 0, 48, 48)`, keyed | Entry `i` of `player_awards` of the row's player, for each of the first three entries that is not -1 | `(268 + 50 * i, 38 + 66 * row, 48, 48)` | The Awards tab is selected | FND-AWARDS-001, FND-AWARDS-004 |
| Statistics strip, per row | `DATA/PX16/PX00201`, source `(96, 112, 160, 64)`, opaque | None | `(262, 30 + 66 * row, 160, 64)` | The Stats tab is selected | FND-AWARDS-003, FND-AWARDS-004 |
| Five statistic values, per row | Font not recorded | The row player's `cash_earned`, `cash_spent`, `damage_inflicted`, `casualties` and `overthrow_count` | Ranked rows: 8 digits at `(371, 37 + 66 * row)`, 8 at `(371, 46 + 66 * row)`, 7 at `(377, 58 + 66 * row)`, 6 at `(383, 67 + 66 * row)`, 6 at `(383, 79 + 66 * row)`. Eliminated rows: all five in 6 digits at x 383, same y | The Stats tab is selected | FND-AWARDS-003, FND-AWARDS-004, SRC-MANUAL-GOG |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Awards | `(428, 33, 48, 48)` | Always | Redraws with the Awards tab, or SCR-AWARDS-002 while one player is active | FND-AWARDS-004, SRC-MANUAL-GOG |
| Stats | `(480, 33, 48, 48)` | Always | Redraws with the Stats tab | FND-AWARDS-004, SRC-MANUAL-GOG |
| Done | `(428, 377, 100, 48)` | Always | Leaves the screen for the title screen on a release inside | FND-AWARDS-003, FND-AWARDS-004, SRC-MANUAL-GOG |

## Keyboard input

None known.

## Other input

| Input | Effect | Evidence |
|---|---|---|
| Menu command `0x81`/9 | Sets the quit flag and leaves | FND-AWARDS-004 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | Awards, Stats or Done is pressed | FND-AWARDS-003, FND-AUDIO-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Awards tab | The screen opens, or Awards is released inside | Stats is released inside | FND-AWARDS-003, FND-AWARDS-004 |
| Stats tab | Stats is released inside | Awards is released inside | FND-AWARDS-003, FND-AWARDS-004 |
| Left | Done is released inside | None | FND-AWARDS-003, SRC-MANUAL-GOG |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- How the number drawer handles a value wider than its field is not recorded;
  an eliminated player's cash or damage of a million or more does not fit its
  six-digit field.
- The text of the score caption at `0x00487704` is not recorded here.
- In 256-colour mode the game uses the `DATA/PX08` files of the same names.
