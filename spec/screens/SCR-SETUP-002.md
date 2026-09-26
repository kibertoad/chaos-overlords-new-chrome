---
id: SCR-SETUP-002
title: Hot-seat handoff card that waits for the next local player to press Ready
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-SETUP-016, FND-OBJECTIVE-004, FND-SETUP-010, FND-OBJECTIVE-002, FND-AUDIO-002, FND-AUDIO-010, SRC-MANUAL-GOG, FND-EXE-004]
conflicting: []
split_with: []
related: [RULE-SETUP-008, RULE-OBJECTIVE-005]
---

## Drawn elements

Positions are on the 640-by-480 screen; the card's picture is composed off
screen and copied to `(0, 30, 640, 400)`. `player` is the slot whose turn
comes next.

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Black background | None | None | The whole screen outside the card | Always | FND-SETUP-016 |
| Handoff card | `DATA/PX16/PX00132` | None | `(266, 130, 108, 164)` | Always | FND-SETUP-010, FND-SETUP-016 |
| Colour bar | None | The player's colour | `(283, 155, 8, 72)` | Always | FND-SETUP-016 |
| Name | None | `player_names[player]`, over a black `(293, 155, 60, 7)` | `(293, 155)` | Always | FND-SETUP-016 |
| Portrait | Interface sheet, source `(32 * portrait, 480, 32, 32)` | `portrait[player]` | `(293, 163, 64, 64)`, scaled | Always | FND-SETUP-016 |
| Ready, pressed | Interface sheet, source `(388, 512, 100, 48)` | None | `(270, 241, 100, 48)` | While Ready is held with the pointer inside | FND-SETUP-016 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Ready | `(270, 241, 100, 48)` | Always | Held-button press; closes the card on a release inside, and the player's planning continues (RULE-SETUP-008) or the elimination card follows (RULE-OBJECTIVE-005) | FND-SETUP-010, FND-SETUP-016 |

## Keyboard input

None known.

## Other input

| Device | Input | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Menu | Commands `0x81`/3, `0x81`/4, `0x81`/9 | Always | The last two can end the match or quit after their confirmations, which closes the card | FND-SETUP-016 |

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | Ready is pressed | FND-AUDIO-010, FND-SETUP-016 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Waiting | RULE-SETUP-008 or RULE-OBJECTIVE-005 shows the card | Ready is released inside, or a menu command ends the match or quits | FND-SETUP-010, FND-SETUP-016 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- Which menu items commands `0x81`/3 and `0x81`/4 are is not recorded.
