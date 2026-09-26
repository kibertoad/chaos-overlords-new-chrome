---
id: SCR-UI-001
title: Title screen
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-UI-009, FND-UI-008, FND-AUDIO-001, FND-PLATFORM-009, FND-UI-021, FND-GFX-004, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-AUDIO-001, SCR-UI-009, SCR-UI-002, RULE-UI-013]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Title art with logo and copyright notice | `DATA/PX16/PX00130` | None | `(0,0,640,460)`, copied opaquely | Always | FND-UI-009 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Menu bar | The window's menu bar | Always | The application menu, SCR-UI-009: New Game leads to setup, Open to loading a game, Host and Join to network play, About to SCR-UI-002 | FND-UI-008 |
| Anywhere in the drawing area | `(0,0,640,460)` | Always | A left press acts as New Game (RULE-UI-013) | FND-PLATFORM-009 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Ctrl+N, Ctrl+O, Ctrl+H, Ctrl+J | Always; Ctrl+H and Ctrl+J only when a transport is chosen | New Game, Open, Host and Join, as the menu items | FND-UI-021 |

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
| Title | At startup, after the intro movies, which are skipped when a game named on the command line is loaded; and when local setup, network setup, loading or a game returns | A menu command or a press starts setup, loading or network play, or Exit | FND-UI-009, FND-UI-008, FND-PLATFORM-009 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- `DATA/PX16/PX00131`, a demo promotion of the same size, has a presenter that
  nothing calls (FND-UI-009), so it is not part of this screen.
