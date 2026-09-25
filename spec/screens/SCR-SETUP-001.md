---
id: SCR-SETUP-001
title: Full local game setup screen with scenario, settings and six player cards
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-SETUP-005, FND-SETUP-002, FND-SETUP-009, FND-SETUP-012, FND-AUDIO-002, FND-AUDIO-010, FND-RNG-005, SRC-MANUAL-GOG, SRC-HELP-GOG]
conflicting: []
split_with: []
related: [RULE-SETUP-002, RULE-SETUP-003, RULE-SETUP-009, RULE-SETUP-010]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Background with the scenario, settings and player panels and the four push buttons | `DATA/PX16/Px00143` | None | `(0, 0, 640, 460)` | Always | FND-AUDIO-010, SRC-MANUAL-GOG |
| Top strip of player portraits, one per slot | `DATA/PX16/PX00129`, the 32-by-32 cell of the slot's `portrait` in row y=480 (cell 15 for an empty slot) | Each slot's `portrait` | Not recorded | Always | FND-SETUP-005, FND-SETUP-002 |
| Player card face, per occupied slot | `DATA/PX16/PX00129`, source `(32 * portrait, 480, 32, 30)`, scaled opaquely | The slot's `portrait` | `(cardX, cardY + 3, 64, 60)` for the slot's card origin | The slot is occupied | FND-SETUP-005 |
| Portrait arrows | `DATA/PX16/PX00140`, source `(220, 138, 64, 62)`, exact white transparent | None | At the selected card's origin, 64 by 62 | On the card of `selected_card` only | FND-SETUP-005 |
| Player name, per occupied slot | Not recorded | The slot's entry of `player_names` | Below the card face | The slot is occupied | FND-SETUP-005, SRC-MANUAL-GOG |
| Scenario selection light | Not recorded | Which of the ten scenarios `scenario` holds | Not recorded | Always | FND-SETUP-012 |
| Drag token | The dragged slot's portrait, 40 by 40 | None | Centred on the pointer, the centre kept within x 20 to 620 and y 20 to 440 | While a card is dragged | FND-SETUP-005 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Player card cell, slot `n` (`n` 0 to 5) | `(397 + 83 * (n % 2), 94 + 74 * (n / 2), 64, 68)` | Always | Press and release per RULE-SETUP-009 | FND-SETUP-005 |
| Portrait down band of a card | `(cellX, cellY, 16, 58)` of the card cell | The card is the selected, occupied, human card | RULE-SETUP-009 | FND-SETUP-005 |
| Portrait up band of a card | `(cellX + 49, cellY, 15, 58)` | The card is the selected, occupied, human card | RULE-SETUP-009 | FND-SETUP-005 |
| Name band of a card | `(cellX, cellY + 58, 64, 10)` | The card is the selected, occupied, human card | RULE-SETUP-009 opens the name editor | FND-SETUP-005 |
| Add Player | `(370, 328, 92, 24)` | Always | RULE-SETUP-010 on release inside | FND-AUDIO-010 |
| Remove Player | `(468, 328, 92, 24)` | Always | RULE-SETUP-010 on release inside | FND-AUDIO-010 |
| Begin | `(370, 375, 92, 45)` | Always | RULE-SETUP-003 on release inside | FND-AUDIO-010, FND-SETUP-002 |
| Cancel | `(468, 375, 92, 45)` | Always | Returns to the title screen on release inside | FND-AUDIO-010, SRC-MANUAL-GOG |
| Scenario, time limit, AI Mentality and turn time buttons | Not recorded | Always | Select the scenario, the time limit, the AI Mentality and the turn time limit | SRC-MANUAL-GOG |

## Keyboard input

None known.

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | Add, Remove, Begin or Cancel is pressed | FND-AUDIO-010 |
| Accepted selection (slot 3) | `DATA/SND00203` | A selector arrow input is accepted | FND-AUDIO-010 |
| Rejected input (slot 4) | `DATA/SND00204` | A selector arrow input is refused, or Add or Remove cannot change the number of players (after the push cue) | FND-AUDIO-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Idle, one local human in slot 0 with portrait 0, card 0 selected, `scenario` from RULE-SETUP-002 | The screen opens | A button or card is pressed | FND-RNG-005, FND-SETUP-005, FND-SETUP-009, SRC-HELP-GOG |
| Button held | Add, Remove, Begin or Cancel is pressed; its pressed image shows while the pointer is inside it and the released image when it leaves | The button is released; the action runs only if the release is inside | FND-AUDIO-010 |
| Pressing a card | A card cell is pressed | The pointer leaves the box from -2 to +1 pixels around the press point (Dragging), or the button is released (RULE-SETUP-009 acts) | FND-SETUP-005 |
| Dragging | The pointer leaves the press box with the button down | The button is released | FND-SETUP-005 |
| Name editor open | RULE-SETUP-009 opens dialog `Chaos Overlords.exe#DIALOG/139` | The dialog is accepted or cancelled | FND-SETUP-005 |
| Left | Begin or Cancel is released inside | None | FND-AUDIO-010 |

## Timing

None known.

## Differences between builds

None known.

## Open questions

- The background image is 640 by 460 pixels (588,854 bytes: a 54-byte header
  and 640 by 460 16-bit pixels). The coordinates here are those of the
  executable's 640-by-460 drawing surface; where that surface sits on the
  640x480 screen (below the window's menu bar) is not recorded.
- The card origins the renderer draws at (`cardX`, `cardY`) are not recorded;
  the input cells start at y 94, 168 and 242, and measurements of the
  background place the faces five pixels higher, which is not confirmed.
- The positions of the top-strip portraits, the name text, the selection
  lights and the input rectangles of the scenario, time limit, AI Mentality
  and turn time buttons are not recorded.
- The pressed images of the four push buttons are taken from
  `DATA/PX16/PX00140`; their source rectangles are not recorded.
- The keyboard handling of the screen is not recorded.
- In 256-colour mode the game uses the `DATA/PX08` files of the same names.
