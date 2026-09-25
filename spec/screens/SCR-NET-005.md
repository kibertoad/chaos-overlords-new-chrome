---
id: SCR-NET-005
title: Legacy network turn synchronization frame with one progress row per seat and a spinner
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-SETUP-007, FND-SETUP-008]
conflicting: []
split_with: []
related: [SCR-NET-004]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Progress frame | `DATA/PX16/PX00139` | None | Taken to be `(210, 60, 220, 72)` | Always | FND-SETUP-007 |
| Progress row, per connection slot `s` (0 to 5) | `DATA/PX16/PX00129`, the green strip from `(354, 0)`, 3 pixels high | The slot's progress | From `(298, 96 + 4 * s)` | The slot is connected and its progress is positive | FND-SETUP-007 |
| Empty row, per connection slot `s` | The panel's empty bar art | None | Row `96 + 4 * s` | The slot is not connected or has no progress | FND-SETUP-007 |
| Status text | Not recorded | A status text of the family used by SCR-NET-004 | Not recorded | Always | FND-SETUP-007 |
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
| Synchronizing; the rows are redrawn as the connected players' turn data arrives | The turn is resolved while the network session state is set, in place of the local whole-turn resolver | The connected players' turns have been exchanged | FND-SETUP-007 |

## Timing

The spinner shows frames 0 to 14 in turn, one per timed update, and returns
to frame 0 after frame 14 [FND-SETUP-008]. The update period is not recorded.

## Differences between builds

None known.

## Open questions

- The frame's screen position is not stated; it is taken to match `PX00137`,
  which has the same size.
- The mapping from progress to bar width is not recorded.
- Where the 640-by-460 drawing surface sits on the 640x480 screen is not
  recorded. In 256-colour mode the game uses the `DATA/PX08` files of the same
  names.
