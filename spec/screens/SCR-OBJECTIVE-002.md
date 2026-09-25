---
id: SCR-OBJECTIVE-002
title: Private elimination card shown to an eliminated local human over the city screen
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-OBJECTIVE-002, FND-AWARDS-003, FND-GFX-003, FND-AUDIO-002, FND-AUDIO-010]
conflicting: []
split_with: []
related: [RULE-OBJECTIVE-005]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| City screen, left as it was | None | None | Whole screen | Always | FND-OBJECTIVE-002 |
| Endgame frame | `DATA/PX16/PX00200` | None | `(106, 25, 428, 410)` | Always | FND-OBJECTIVE-002, FND-GFX-003 |
| Elimination splash | `DATA/PX16/PX00203` | None | From `(110, 30)` | Always | FND-OBJECTIVE-002 |
| Player name | Font not recorded | `player_names` of the eliminated player, unchanged | Centred on `(158, 46)` | Always | FND-OBJECTIVE-002 |
| Overlord portrait | Source not recorded | The eliminated player's portrait | `(126, 54, 64, 64)` | Always | FND-OBJECTIVE-002 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Done | `(428, 377, 100, 48)` | Always | Closes the card on a release inside; RULE-OBJECTIVE-005 retires the slot | FND-OBJECTIVE-002, FND-AWARDS-003 |
| Rest of the card | Not applicable | Always | Nothing | FND-OBJECTIVE-002 |

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
| Waiting | RULE-OBJECTIVE-005 shows the card | Done is released inside | FND-OBJECTIVE-002 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The size of `PX00203`, the portrait's source sheet and cell, and the name's
  font are not recorded.
- Whether Done shows a pressed image, and whether the push cue plays here as it
  does on SCR-AWARDS-001, is inferred from the shared Done rectangle and not
  recorded for this card.
- Whether the Awards and Stats controls of the frame react on this card is not
  recorded.
- In 256-colour mode the game uses the `DATA/PX08` files of the same names.
