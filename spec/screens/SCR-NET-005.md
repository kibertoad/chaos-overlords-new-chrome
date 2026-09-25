---
id: SCR-NET-005
title: Legacy network turn synchronization frame with one progress row per seat and a spinner
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-NET-003, FND-SETUP-007, FND-SETUP-008, FND-NET-002]
conflicting: []
split_with: []
related: [SCR-NET-004]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Progress frame | `DATA/PX16/PX00139` | None | `(210, 60, 220, 72)` | Always | FND-SETUP-007, FND-NET-003 |
| Progress row, per player slot `s` (0 to 5) | `DATA/PX16/PX00129`, the green strip from `(354, 0)`, 3 pixels high | The slot's progress value, one pixel per unit | From `(298, 96 + 4 * s)` | The slot's `controller` is 3 (a remote human) and its progress is positive | FND-SETUP-007, FND-NET-002 |
| Empty row, per player slot `s` | `DATA/PX16/PX00129` `(360, 6, 100, 3)` | None | `(298, 96 + 4 * s, 100, 3)` | Every other slot | FND-SETUP-007, FND-NET-002 |
| Status text | String-table entries `0x4D + 2 * code` and `0x4E + 2 * code` for codes 0 to 4, `0x43 + 2 * code` and `0x44 + 2 * code` for codes 10 to 13 | A two-line status message | Two lines centred on x 347, on rows 76 and 84, over a black band `(278, 76, 138, 16)` | When the caller passes a code other than -1 | FND-SETUP-007, FND-NET-002 |
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
| Synchronizing; the rows are redrawn as the connected players' turn data arrives | The turn is resolved while the network session state is set, in place of the local whole-turn resolver | The connected players' turns have been exchanged | FND-SETUP-007 |

## Timing

The spinner shows frames 0 to 14 in turn, one per timed update, and returns
to frame 0 after frame 14 [FND-SETUP-008]. It advances on each raised timer-0
flag, every 166 ms, about six frames a second [FND-NET-003].

## Differences between builds

None known.

## Open questions

- The session setup starts each remote seat at 0 and every other seat at 100,
  and sets a seat to 100 when its connection finishes (FND-NET-003); the
  values passed in between have not been traced.
- Where the 640-by-460 drawing surface sits on the 640x480 screen is not
  recorded. In 256-colour mode the game uses the `DATA/PX08` files of the same
  names.
