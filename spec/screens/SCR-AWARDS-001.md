---
id: SCR-AWARDS-001
title: Endgame screen listing the players by place with their awards or their statistics
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-AWARDS-003, FND-AWARDS-001, FND-GFX-003, FND-AUDIO-002, FND-AUDIO-010, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-AWARDS-001, RULE-AWARDS-002]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| City screen, left as it was | None | None | Whole screen | Always | FND-AWARDS-003 |
| Endgame frame with the Awards, Stats and Done controls | `DATA/PX16/PX00200` | None | `(106, 25, 428, 410)` | Always | FND-AWARDS-003, FND-GFX-003 |
| Place marker, per displayed row `row` | `DATA/PX16/PX00201`, source `(16 * (place - 1), 48 + 32 * player, 16, 32)` | The row player's place, `scenario_standing + 1` from RULE-AWARDS-002 | `(113, 31 + 66 * row, 16, 32)` | The row's player is active | FND-AWARDS-003 |
| Player name, per displayed row | Font not recorded | `player_names` of the row's player, unchanged and with no place prefix | From `(197, 38 + 66 * row)` | Always | FND-AWARDS-003 |
| Award icons, per displayed row | `DATA/PX16/PX00201`, one icon per award category | The first three entries of `player_awards` of the row's player, set by RULE-AWARDS-001 | Not recorded | The Awards tab is selected | FND-AWARDS-001 |
| Statistics strip, per displayed row | `DATA/PX16/PX00201`, source `(96, 112, 160, 64)`, opaque | None | `(262, 30 + 66 * row, 160, 64)` | The Stats tab is selected | FND-AWARDS-003 |
| Five statistic values, per displayed row | Font not recorded | The row player's `cash_earned`, `cash_spent`, `damage_inflicted`, `casualties` and `overthrow_count`, in fixed-width fields of 8, 8, 7, 6 and 6 digits | Each field ends at x 419, at y offsets 37, 46, 58, 67 and 79 from the row's top | The Stats tab is selected | FND-AWARDS-003, SRC-MANUAL-GOG |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Awards | Not recorded | Always | Selects the Awards tab | FND-AWARDS-003, SRC-MANUAL-GOG |
| Stats | Not recorded | Always | Selects the Stats tab | FND-AWARDS-003, SRC-MANUAL-GOG |
| Done | `(428, 377, 100, 48)` | Always | Leaves the screen for the title screen on a release inside | FND-AWARDS-003, SRC-MANUAL-GOG |

## Keyboard input

None known.

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | Awards, Stats or Done is pressed | FND-AWARDS-003, FND-AUDIO-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Awards tab | Not recorded | Stats is released inside | FND-AWARDS-003 |
| Stats tab | Stats is released inside | Awards is released inside | FND-AWARDS-003 |
| Left | Done is released inside | None | FND-AWARDS-003, SRC-MANUAL-GOG |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The rows follow `endgame_rows` from RULE-AWARDS-002; the renderer's own
  ordering code is not recorded.
- Which statistic each vertical offset holds is not recorded. SRC-MANUAL-GOG
  lists them as Cash Earned, Cash Spent, Damage Inflicted, Casualties and
  Overthrows, which `cash_earned`, `cash_spent`, `damage_inflicted`,
  `casualties` and `overthrow_count` hold. The rule that formats them is not
  recorded.
- Whether the y offsets of the statistics are measured from `30 + 66 * row`
  is not stated.
- The Awards and Stats rectangles are not recorded; measurements of the frame
  put them at `(428, 33, 48, 48)` and `(480, 33, 48, 48)`, not confirmed from
  the executable. The source cells of the award icons and pressed controls in
  `PX00201`, the icons' positions, and which tab is selected first are not
  recorded.
- In 256-colour mode the game uses the `DATA/PX08` files of the same names.
