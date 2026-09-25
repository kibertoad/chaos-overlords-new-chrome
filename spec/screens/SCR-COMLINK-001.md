---
id: SCR-COMLINK-001
title: Comlink View panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-COMLINK-002, FND-COMLINK-004, FND-AUDIO-002, FND-AUDIO-011, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-COMLINK-004, RULE-COMLINK-005]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05017` | None | (104, 124, 344, 209) | While the panel is open | FND-COMLINK-002 |
| Page header | Font not recorded | The one-based number of the message shown and `comlink_count`, formatted by RULE-COMLINK-005 | Not recorded | While the panel is open | FND-COMLINK-004 |
| Date | Font not recorded | The message's `turn` as year and week, formatted by RULE-COMLINK-005 | Not recorded | While the panel is open | FND-COMLINK-004 |
| Sender name | Font not recorded | The name in `player_names` of the message's `sender`, up to 10 characters | Not recorded | While the panel is open | FND-COMLINK-004 |
| Sender portrait | The sender's 32-by-32 portrait and colour; resource not recorded | None | Not recorded | While the panel is open | FND-COMLINK-004 |
| Message | Font not recorded | The four 40-character rows of the message's `text`, one per line | Not recorded | While the panel is open | FND-COMLINK-004 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Panel | (104, 124, 344, 209) | Always | Pointer releases outside are moved to the nearest point inside before the controls are tested | FND-COMLINK-002 |
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

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` | A step to another message, before the pressed arrow is drawn | FND-AUDIO-011 |
| Rejected input | `DATA/SND00204` | Previous at the first message or Next at the last; also when the View half of the Comlink control is pressed with no messages, and the panel then does not open | FND-COMLINK-002, FND-AUDIO-011 |

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

- The positions and fonts of the header, date, name, portrait and message rows
  are not recorded.
- FND-COMLINK-004 calls the sender's picture a 32-by-32 portrait; whether it
  is drawn at that size is not recorded.
- After each step the region (104, 124, 344, 209) is copied to the screen
  again; how long a pressed arrow stays drawn is not recorded.
- The resource and position of the pressed arrow and Dismiss faces are not
  recorded.
