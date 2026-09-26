---
id: SCR-NET-004
title: Legacy network transfer progress frame with a status line and a spinner
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-NET-003, FND-SETUP-007, FND-SETUP-008]
conflicting: []
split_with: []
related: []
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Progress frame | `DATA/PX16/PX00137` | None | `(210, 60, 220, 72)` | Always | FND-SETUP-007 |
| Progress bar | `DATA/PX16/PX00129`, the green strip from `(354, 0)`, 3 pixels high | The transfer's progress, one pixel per unit: four times the number of 25 blocks received so far, and 100 at the end | `(298, 105, progress, 3)` | Always | FND-SETUP-007, FND-NET-003 |
| Status text | Not recorded | One of the status text pairs for modes 0 to 4 and 10 to 13 | Not recorded | Always | FND-SETUP-007 |
| Spinner | `DATA/PX16/PX00138`, cell `(48 * frame, 0, 48, 48)` | None | `(224, 72, 48, 48)` | Always | FND-SETUP-008, FND-NET-003 |

## Mouse input

None known.

## Keyboard input

None known.

## Other input

None.

## Sounds

None known.

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Transferring; the bar and status text are redrawn as data arrives | A network connection or setup path starts a file transfer | The transfer ends | FND-SETUP-007 |

## Timing

The spinner shows frames 0 to 14 in turn, one per timed update, and returns
to frame 0 after frame 14 [FND-SETUP-008]. It advances on each raised timer-0
flag, every 166 ms, about six frames a second [FND-NET-003].

## Differences between builds

None known.

## Open questions

- The status texts, their positions, and whether the transfer can be
  cancelled from this frame are not recorded.
- Where the 640-by-460 drawing surface sits on the 640x480 screen is not
  recorded. In 256-colour mode the game uses the `DATA/PX08` files of the same
  names.
