---
id: SCR-NET-004
title: Legacy network transfer progress frame with a status line and a spinner
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-SETUP-007, FND-SETUP-008]
conflicting: []
split_with: []
related: []
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Progress frame | `DATA/PX16/PX00137` | None | `(210, 60, 220, 72)` | Always | FND-SETUP-007 |
| Progress bar | `DATA/PX16/PX00129`, the green strip from `(354, 0)`, 3 pixels high | The transfer's progress, as the width of the copied segment | From `(298, 105)` | Always | FND-SETUP-007 |
| Status text | Not recorded | One of the status text pairs for modes 0 to 4 and 10 to 13 | Not recorded | Always | FND-SETUP-007 |
| Spinner | `DATA/PX16/PX00138`, the 48-by-48 cell of the current frame | None | `(224, 72, 48, 48)` | Always | FND-SETUP-008 |

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
to frame 0 after frame 14 [FND-SETUP-008]. The update period is not recorded.

## Differences between builds

None known.

## Open questions

- How the progress value maps to a bar width, and the full bar width, are not
  recorded.
- Spinner cell `n` is taken to be `(48 * n, 0, 48, 48)` of the 720-by-48
  sheet.
- The status texts, their positions, and whether the transfer can be
  cancelled from this frame are not recorded.
- Where the 640-by-460 drawing surface sits on the 640x480 screen is not
  recorded. In 256-colour mode the game uses the `DATA/PX08` files of the same
  names.
