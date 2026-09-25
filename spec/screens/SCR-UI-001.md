---
id: SCR-UI-001
title: Title screen
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-009, FND-UI-008, FND-AUDIO-001]
conflicting: []
split_with: []
related: [RULE-AUDIO-001, SCR-UI-009, SCR-UI-002]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Title art with logo and copyright notice | `DATA/PX16/PX00130` | None | `(0,0,640,460)`, copied opaquely | Always | FND-UI-009 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Menu bar | The window's menu bar | Always | The application menu, SCR-UI-009: New Game leads to setup, Open to loading a game, Host and Join to network play, About to SCR-UI-002 | FND-UI-008 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| None known | | | FND-UI-009 |

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| None | | | | FND-UI-009 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Title music | CD audio track 2, repeated (RULE-AUDIO-001 mode 0) | On first entry and on every return from setup, network setup or loading | FND-AUDIO-001 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Title | At startup, and when local setup, network setup or loading returns | A menu command starts setup, loading or network play, or Exit | FND-UI-009, FND-UI-008 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The title shows no controls of its own; whether it takes any key or click
  beyond the menu bar is not recorded.
- The drawn area is 640 by 460 below the window's menu bar; how that maps onto
  the 640-by-480 canvas is not recorded.
- `DATA/PX16/PX00131`, a demo promotion of the same size, has a presenter that
  nothing calls (FND-UI-009), so it is not part of this screen.
- Whether the startup movies play before the title on every start is not
  recorded here.
