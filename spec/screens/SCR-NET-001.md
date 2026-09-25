---
id: SCR-NET-001
title: Legacy network host lobby that edits up to four seats and waits for the participants
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-NET-003, FND-SETUP-006, FND-SETUP-005, FND-AUDIO-002, FND-AUDIO-010, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [SCR-SETUP-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Background | `DATA/PX16/PX00144` | None | `(0, 0, 640, 460)` | Always | FND-SETUP-006 |
| Seat card, per configured seat | Drawn as the player card of SCR-SETUP-001 | The seat's portrait and name | The seat cell's origin | The seat is configured | FND-SETUP-006, FND-SETUP-005 |
| Host address message | Not recorded | The host's network address | Not recorded | Once, after the host-side channels start | FND-SETUP-006 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Seat cell, seat `n` (`n` 0 to 3) | `(397 + 83 * (n % 2), 94 + 74 * (n / 2), 64, 68)` | Always | Selects the seat; its portrait and name bands work as on SCR-SETUP-001 but change the network session's seat | FND-SETUP-006, FND-SETUP-005 |
| Add | `(371, 254, 92, 24)`; its pressed image, surface 7 `(220, 0, 92, 24)`, is drawn at x 370 | Always | With fewer than four local seats, makes the lowest empty slot a local seat with the lowest free portrait; otherwise the rejection cue | FND-NET-003 |
| Remove | `(468, 254, 92, 24)`, pressed image `(220, 24, 92, 24)` | Always | With at least two local seats, empties the last one added; otherwise the rejection cue | FND-NET-003 |
| Begin | `(370, 375, 92, 45)`, pressed image `(220, 48, 92, 45)` | Always | Marks the local seats ready when the last remote seat in slot order is ready; otherwise the rejection cue | FND-NET-003 |
| Cancel | `(468, 375, 92, 45)`, pressed image `(220, 93, 92, 45)` | Always | Closes all twelve connections and leaves | FND-NET-003 |

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
| Editing, one seat configured; the participants' states are polled and the game cannot start until they allow it | The screen opens | Every slot's ready byte is set, and the session starts with the slots still showing portrait 15 made computer players; or the host leaves | FND-SETUP-006, FND-NET-003 |
| Action held; its pressed image shows while the pointer stays inside | An action is pressed | The button is released; the action runs only on a release inside | FND-SETUP-006 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- Begin tests only the last remote seat's ready byte; whether that is
  intended is not settled (FND-NET-003).
- The keyboard handling and the message that gives the host address are not
  recorded.
- The background image is 640 by 460 pixels; where it sits on the 640x480
  screen is not recorded. In 256-colour mode the game uses `DATA/PX08/PX00144`.
