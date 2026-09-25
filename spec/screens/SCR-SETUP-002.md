---
id: SCR-SETUP-002
title: Hot-seat handoff card that waits for the next local player to press Ready
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-SETUP-010, FND-OBJECTIVE-002, FND-AUDIO-002, FND-AUDIO-010, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-SETUP-008, RULE-OBJECTIVE-005]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Handoff card | `DATA/PX16/PX00132` | None | Not recorded | Always | FND-SETUP-010 |
| Next player's portrait | Not recorded | The Overlord portrait of the player whose turn comes next | Not recorded | Always | SRC-MANUAL-GOG |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Ready | Not recorded | Always | Closes the card on release inside; the player's planning continues (RULE-SETUP-008) or the elimination card follows (RULE-OBJECTIVE-005) | FND-SETUP-010, FND-OBJECTIVE-002 |

## Keyboard input

None known.

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | Ready is pressed | FND-AUDIO-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Waiting | RULE-SETUP-008 or RULE-OBJECTIVE-005 shows the card | Ready completes, or the menu or quit state interrupts it | FND-SETUP-010 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The card's position, the portrait's source and position, and the Ready
  rectangle are not recorded.
- Whether any key completes Ready is not recorded; the finding says neither
  the keyboard nor the pointer path continues until Ready completes.
