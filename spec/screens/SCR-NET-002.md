---
id: SCR-NET-002
title: Legacy network client session editor with four seats
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-SETUP-006, FND-SETUP-005, FND-AUDIO-002, FND-AUDIO-010, FND-NET-001]
conflicting: []
split_with: []
related: [SCR-SETUP-001, SCR-NET-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Background | `DATA/PX16/PX00145` | None | `(0, 0, 640, 460)` | After the connection opens | FND-SETUP-006 |
| Seat card, per configured seat | Drawn as the player card of SCR-SETUP-001 | The seat's portrait and name | The seat cell's origin | The seat is configured | FND-SETUP-006, FND-SETUP-005 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Seat cell, seat `n` (`n` 0 to 3) | `(251 + 83 * (n % 2), 124 + 74 * (n / 2), 64, 68)` | Always | Selects the seat; its portrait and name bands work as on SCR-SETUP-001 but change the network session's seat | FND-SETUP-006, FND-SETUP-005 |
| Action 1 | Recorded as `(284, 225, 24, 92)` | Always | Not recorded | FND-SETUP-006 |
| Action 2 | Recorded as `(284, 322, 24, 92)` | Always | Not recorded | FND-SETUP-006 |
| Action 3 | Recorded as `(345, 224, 45, 92)` | Always | Not recorded | FND-SETUP-006 |
| Action 4 | Recorded as `(345, 322, 45, 92)` | Always | Not recorded | FND-SETUP-006 |

## Keyboard input

None known.

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | An action is pressed | FND-SETUP-006, FND-AUDIO-010 |
| Rejected input (slot 4) | `DATA/SND00204` | After the push cue, when an operation on the seat count is refused | FND-SETUP-006, FND-AUDIO-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Negotiating one of three connection modes, before the background is loaded | The screen is chosen from the title | A connection opens | FND-SETUP-006 |
| Editing | The connection opens and the background is loaded | The session starts or the player leaves | FND-SETUP-006 |
| Action held; its pressed image shows while the pointer stays inside | An action is pressed | The button is released; the action runs only on a release inside | FND-SETUP-006 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The order of the four numbers in each action rectangle is not settled (see
  SCR-NET-001), and which action does what is not recorded.
- The seat cell step of 83 by 74 pixels is taken from the four recorded
  origins.
- How the three connection modes are offered and chosen is not recorded.
- The host handles a joining computer's portrait steps as packet types 3 and 4
  and keeps the seats, portraits and names; the joining side receives them as
  packet types 0, 2 and 15 (FND-NET-001). Which control on this screen sends
  each packet has not been traced.
- The background image is 640 by 460 pixels; where it sits on the 640x480
  screen is not recorded. In 256-colour mode the game uses `DATA/PX08/PX00145`.
