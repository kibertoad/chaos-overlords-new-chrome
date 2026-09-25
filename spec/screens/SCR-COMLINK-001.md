---
id: SCR-COMLINK-001
title: Comlink View panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-COMLINK-002, FND-COMLINK-004, FND-COMLINK-007, FND-COMLINK-009, FND-EVENT-005, FND-AUDIO-002, FND-AUDIO-011, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-COMLINK-001, RULE-COMLINK-004, RULE-COMLINK-005]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05017` | None | (104, 124, 344, 209) | While the panel is open | FND-COMLINK-002 |
| Message number | Digits of `DATA/PX16/PX00129` | `comlink_cursor + 1`, two digits with a leading zero | (138, 137) | While the panel is open | FND-COMLINK-007 |
| Message count | Digits of `DATA/PX16/PX00129` | `comlink_count`, two digits with a leading zero | (174, 137) | While the panel is open | FND-COMLINK-007 |
| Previous | `DATA/PX16/PX00129` rectangle (118, 363, 26, 23), or (170, 363, 26, 23) at the first message | None | (135, 157, 26, 23) | While the panel is open | FND-COMLINK-007, FND-EVENT-005 |
| Next | `DATA/PX16/PX00129` rectangle (144, 363, 26, 23), or (196, 363, 26, 23) at the last message | None | (163, 157, 26, 23) | While the panel is open | FND-COMLINK-007, FND-EVENT-005 |
| Previous pressed, Next pressed | `DATA/PX16/PX00129` rectangles (66, 363, 26, 23) and (92, 363, 26, 23) | None | Over Previous and Next | While the arrow is held with the pointer inside it, or briefly after Left or Right | FND-COMLINK-007, FND-EVENT-005 |
| Dismiss pressed | `DATA/PX16/PX00129` rectangle (50, 386, 50, 23) | None | (137, 293, 50, 23) | While Dismiss is held with the pointer inside it | FND-COMLINK-007 |
| Year | Digits of `DATA/PX16/PX00129` | `2050 + turn / 52` of the message, four digits | (199, 144) | While the panel is open | FND-COMLINK-004, FND-COMLINK-007 |
| Week | Digits of `DATA/PX16/PX00129` | `turn % 52 + 1`, two digits with a leading zero | (229, 144) | While the panel is open | FND-COMLINK-004, FND-COMLINK-007 |
| Name backing | Black fill | None | (199, 162, 60, 7) | While the panel is open | FND-COMLINK-007 |
| Sender name | Character cells of `DATA/PX16/PX00129` | The first 10 characters of the name in `player_names` of the message's `sender` | (199, 162) | While the panel is open | FND-COMLINK-004, FND-COMLINK-007 |
| Sender colour | Fill in the sender's colour | None | (199, 170, 8, 64) | While the panel is open | FND-COMLINK-007 |
| Sender portrait | `DATA/PX16/PX00129` rectangle (`portrait * 32`, 480, 32, 32) of the sender, stretched by `StretchBlt` in `COLORONCOLOR` mode | None | (207, 170, 64, 64) | While the panel is open | FND-COMLINK-007 |
| Message | Character cells of `DATA/PX16/PX00129` | Row `r` of the message's `text`, 40 characters | (199, 245 + 8 * r) for `r` 0 to 3 | While the panel is open | FND-COMLINK-004, FND-COMLINK-007 |
## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Outside the panel | Anything outside (104, 124, 344, 209) | Always | A press or double-click plays the rejected-input sound | FND-COMLINK-007 |
| Previous | (135, 157, 26, 23) | Always | Subtracts 1 from `comlink_cursor` and shows the message through RULE-COMLINK-005; at the first message, changes nothing and plays the rejected-input sound | FND-COMLINK-002, FND-AUDIO-011 |
| Next | (163, 157, 26, 23) | Always | Adds 1 to `comlink_cursor` and shows the message through RULE-COMLINK-005; at the last message, changes nothing and plays the rejected-input sound | FND-COMLINK-002, FND-AUDIO-011 |
| Dismiss | (137, 293, 49, 22) | Always | Closes the panel | FND-COMLINK-002 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Left | Always | As Previous | FND-COMLINK-002 |
| Right | Always | As Next | FND-COMLINK-002 |
| Enter | Always | Presses Dismiss | FND-COMLINK-002 |
| Execute (virtual key `0x2B`) | Always | Presses Dismiss | FND-COMLINK-002 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Network | A message stored for the viewing player (RULE-COMLINK-001), which sets `comlink_view_refresh` | While open | After the event that stored it, redraws the message at `comlink_cursor` with the new count and copies the panel to the screen again; the new message is not shown, and the shown one changes only when it was dropped from a full inbox | FND-COMLINK-009 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` | A step to another message, before the pressed arrow is drawn | FND-AUDIO-011 |
| Rejected input | `DATA/SND00204` | Previous at the first message or Next at the last; a press outside the panel; also when the View half of the Comlink control is pressed with no messages, and the panel then does not open | FND-COMLINK-002, FND-COMLINK-007, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Showing message `comlink_cursor` | RULE-COMLINK-004 opens the panel, or a legal step | Another step, or the panel closes | FND-COMLINK-002, FND-COMLINK-004 |
| Closed | Dismiss is released inside itself, or Enter or Execute is pressed | The View half of the Comlink control is pressed with at least one message | FND-COMLINK-002 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- After each step the region (104, 124, 344, 209) is copied to the screen
  again; how long a pressed arrow stays drawn after Left or Right depends on
  a wait that was not read.
