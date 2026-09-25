---
id: SCR-UI-002
title: Credits screen
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-007, FND-UI-008, FND-EXE-004]
conflicting: []
split_with: []
related: [SCR-UI-009]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Publisher and developer credits | `DATA/PX16/Px00100` | None | `(0,0,640,460)`, copied opaquely | Always; redrawn on each paint message | FND-UI-007 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Whole screen | `(0,0,640,460)` | Always | A click closes the screen and returns to the screen it was opened from | FND-UI-007 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Any key | Always | Closes the screen and returns to the screen it was opened from | FND-UI-007 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Window | Messages the event layer numbers 17 and 18 | Always | Close the screen | FND-UI-007 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| None | | | FND-UI-007 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Credits | The Help menu's About Chaos Overlords command (`0x8003`) on any screen | A click, a key or a closing window message; the previous screen's surfaces are then restored | FND-UI-007, FND-UI-008 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- Which Windows messages the event layer's numbers 2, 3, 4, 17 and 18 stand for;
  the table reads 2 to 4 as mouse and key presses.
- Whether the music goes on playing unchanged, as no music call is recorded on
  this path.
