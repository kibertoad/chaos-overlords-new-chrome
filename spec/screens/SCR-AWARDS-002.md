---
id: SCR-AWARDS-002
title: Victory splash shown before the endgame results when one human plays
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-AWARDS-003, FND-OBJECTIVE-002, FND-AUDIO-002, FND-AUDIO-010]
conflicting: []
split_with: []
related: [RULE-AWARDS-002, SCR-AWARDS-001]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Endgame frame | `DATA/PX16/PX00200` | None | `(106, 25, 428, 410)` | Always | FND-AWARDS-003, FND-OBJECTIVE-002 |
| Victory splash | `DATA/PX16/PX00202` | None | Not recorded | Always | FND-AWARDS-003 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Done | Taken to be `(428, 377, 100, 48)` | Always | Continues to SCR-AWARDS-001 | FND-AWARDS-003 |

## Keyboard input

None known.

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | Done is pressed | FND-AWARDS-003, FND-AUDIO-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Waiting | RULE-AWARDS-002 shows the splash | The splash is dismissed | FND-AWARDS-003 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- Where `PX00202` is placed is not recorded; the elimination splash of the
  same family is drawn from `(110, 30)`, with the player's name at
  `(158, 46)` and a 64-by-64 portrait at `(126, 54)`, and this splash may use
  the same layout.
- How the splash is dismissed, and whether the results follow it, is not
  recorded.
- In 256-colour mode the game uses the `DATA/PX08` files of the same names.
