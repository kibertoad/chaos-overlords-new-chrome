---
id: SCR-AWARDS-002
title: Victory splash shown on the endgame's Awards tab when one player is left
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-AWARDS-004, FND-AWARDS-003, FND-OBJECTIVE-002, FND-AUDIO-002, FND-AUDIO-010]
conflicting: []
split_with: []
related: [RULE-AWARDS-002, SCR-AWARDS-001]
---

## Drawn elements

`survivor` is the last active slot (RULE-AWARDS-002).

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Endgame frame | `DATA/PX16/PX00200` | None | `(106, 25, 428, 410)` | Always | FND-AWARDS-003, FND-AWARDS-004 |
| Tab mark | Interface sheet, source `(488, 512, 8, 16)` | Awards tab | `(468, 33, 8, 16)` | Always | FND-AWARDS-004 |
| Victory splash | `DATA/PX16/PX00202` | None | `(110, 30, 311, 393)` | Always | FND-AWARDS-004 |
| Colour fills | None | The survivor's colour | `(110, 30, 40, 12)`, `(110, 42, 13, 79)`, `(110, 121, 40, 302)` | Always | FND-AWARDS-004 |
| Name | None | `player_names[survivor]` | `(158 - 3 * length, 46)` | Always | FND-AWARDS-004 |
| Portrait | Interface sheet, source `(32 * portrait, 480, 32, 32)` | `portrait[survivor]` | `(126, 54, 64, 64)`, scaled | Always | FND-AWARDS-004 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Done | `(428, 377, 100, 48)` | Always | Leaves the endgame | FND-AWARDS-004 |
| Awards tab | `(428, 33, 48, 48)` | Always | Draws the splash again | FND-AWARDS-004 |
| Stats tab | `(480, 33, 48, 48)` | Always | Shows the Stats view of SCR-AWARDS-001 | FND-AWARDS-004 |

## Keyboard input

None known.

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Menu | Command `0x81`/9 | Always | Sets the quit flag and leaves | FND-AWARDS-004 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | Done is pressed | FND-AWARDS-003, FND-AUDIO-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Waiting | The endgame opens with one active player, or the Awards tab is pressed then | Done, or the Stats tab | FND-AWARDS-004 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- Whether the tab buttons play a sound is not recorded.
- In 256-colour mode the game uses the `DATA/PX08` files of the same names.
