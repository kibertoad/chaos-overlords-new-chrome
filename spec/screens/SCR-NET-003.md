---
id: SCR-NET-003
title: Legacy network screen that waits for every participant to be ready
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-NET-003, FND-SETUP-006, FND-AUDIO-002, FND-AUDIO-010, FND-EXE-004]
conflicting: []
split_with: []
related: []
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Background | `DATA/PX16/PX00146` | None | `(0, 0, 640, 460)` | Always | FND-SETUP-006 |
| Six-seat strip, drawn unscaled | Not recorded | Each seat's participant | Not recorded | Always | FND-SETUP-006 |
| Control images | Surface 1 for the released image, surface 7 for the pressed image | None | At the two control rectangles | Always | FND-SETUP-006 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Continue | `(224, 230, 92, 45)` | Always | Continues the session on a release inside | FND-SETUP-006, FND-NET-003 |
| Cancel | `(322, 230, 92, 45)`, beside Continue | Always | Closes the session and all twelve tracked connections and returns on a release inside | FND-SETUP-006, FND-NET-003 |

## Keyboard input

None known.

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | A control is pressed | FND-SETUP-006, FND-AUDIO-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Waiting; arriving connection records are processed and the seat strip is redrawn | The screen opens | A control is released inside | FND-SETUP-006 |
| Control held; the pressed image shows while the pointer stays inside | A control is pressed | The button is released | FND-SETUP-006 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The seat strip's position and cell sources are not recorded.
- Whether the rejection cue plays on this screen is not recorded.
- The background image is 640 by 460 pixels; where it sits on the 640x480
  screen is not recorded. In 256-colour mode the game uses `DATA/PX08/Px00146`.
