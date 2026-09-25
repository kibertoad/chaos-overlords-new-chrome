---
id: SCR-SETUP-001
title: Full local game setup screen with scenario, settings and six player cards
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-SETUP-013, FND-OBJECTIVE-003, FND-SETUP-005, FND-SETUP-014, FND-SETUP-002, FND-SETUP-009, FND-SETUP-012, FND-AUDIO-002, FND-AUDIO-010, FND-RNG-005, SRC-MANUAL-GOG, SRC-HELP-GOG]
conflicting: []
split_with: []
related: [RULE-SETUP-002, RULE-SETUP-003, RULE-SETUP-009, RULE-SETUP-010]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Background with the scenario, settings and player panels and the four push buttons | `DATA/PX16/Px00143` | None | `(0, 0, 640, 460)` | Always | FND-AUDIO-010, SRC-MANUAL-GOG |
| Top strip of player portraits, one per slot | `DATA/PX16/PX00129`, the 32-by-32 cell of the slot's `portrait` in row y=480 (cell 15 for an empty slot) | Each slot's `portrait` | `(360 + 36 * n, 38, 32, 32)` for slot `n` | Always | FND-SETUP-005, FND-SETUP-002, FND-SETUP-014 |
| Player card, per human slot | Composed from the background, a colour bar, the portrait and the name, then copied opaquely | The slot's colour, `portrait` and name | `(cardX, cardY - 3, 76, 68)`, with card origins `(385,95)`, `(468,95)`, `(385,169)`, `(468,169)`, `(385,243)` and `(468,243)` for slots 0 to 5 | The slot's `controller` is 0 or 3; for -1 and 1 the background is restored there instead | FND-SETUP-014 |
| Colour bar | Fill in the slot's colour | The slot's colour | `(cardX, cardY, 9, 41)` | On each card | FND-SETUP-014 |
| Player card face, per card | `DATA/PX16/PX00129`, source `(32 * portrait, 480, 32, 30)`, scaled opaquely | The slot's `portrait` | `(cardX + 12, cardY - 3, 64, 60)` | On each card | FND-SETUP-005, FND-SETUP-014 |
| Portrait arrows | `DATA/PX16/PX00140`, source `(220, 138, 64, 62)`, exact white transparent | None | Over the card face, `(cardX + 12, cardY - 3, 64, 62)` | On the card of `selected_card` only | FND-SETUP-005, FND-SETUP-014 |
| Player name, per card | The font of `fn_00413FD5` | The slot's entry of `player_names` | Centred on x `cardX + 45`, starting at `cardX + 45 - 3 * length`, on row `cardY + 58` | On each card | FND-SETUP-005, FND-SETUP-014, SRC-MANUAL-GOG |
| Scenario title | String resource `scenario + 1` of `Chaos Overlords.exe`, over a black `(84, 40, 216, 52)` | The scenario's name | `(84, 40)` | Always | FND-SETUP-013 |
| Scenario description | String resource `scenario + 95`, broken at the last space before 36 characters, at most five lines | The scenario's objective | `(84, 52 + 8 * line)` | Always | FND-SETUP-013 |
| Selection lights | `DATA/PX16/PX00140`, source `(304, 138, 8, 16)`, drawn after the left half `(0, 0, 320, 460)` is restored from the background | `scenario`, `turn_limit`, `mentality`, `planning_limit_choice` | Scenario: x 182 for an even value and 296 for an odd one, y 109 for 4 and 5, 144 for 6 and 7, 179 for 8 and 9, 215 for 0 and 1, 250 for 2 and 3. Time limit: `(125, 285)`, `(182, 285)`, `(239, 285)`, `(296, 285)` for 26, 52, 104, 208. `(182, 337 + 27 * mentality)`. `(296, 337 + 27 * planning_limit_choice)` | The time-limit light only while `scenario` is below 4 | FND-SETUP-012, FND-SETUP-013 |
| Pressed left-panel button | `DATA/PX16/PX00140`: scenario button `k` from `(110 * (k % 2), 32 * (k / 2), 110, 32)`, time limit `i` from `(53 * i, 256, 53, 24)`, mentality row `i` from `(0, 160 + 24 * i, 110, 24)`, turn time row `i` from `(110, 160 + 24 * i, 110, 24)` | None | Over the button's rectangle | While the button is held with the pointer inside | FND-SETUP-013 |
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
| Scenario button `k` (0 to 9) | `(80 + 114 * (k % 2), 109 + 35 * (k / 2), 110, 32)` for `k` below 6, and `(80 + 114 * (k % 2), 110 + 35 * (k / 2), 110, 32)` from 6 | Always | On release inside, `scenario` becomes `k + 4` for `k` below 6 and `k - 6` from 6, and is stored as `preferred_scenario` | FND-SETUP-013, FND-OBJECTIVE-003 |
| Time limit `i` (26, 52, 104, 208) | `(80 + 57 * i, 285, 53, 23)` | `scenario` is below 4 | On release inside, `turn_limit` takes the value | FND-SETUP-013 |
| Time limit area | `(80, 284, 224, 24)` | `scenario` is 4 or more | Plays the rejection sound, slot 4 | FND-SETUP-013 |
| AI Mentality row `i` (0 to 3) | `(80, 337 + 27 * i, 110, 24)` | Always | On release inside, `mentality` becomes `i` | FND-SETUP-013 |
| Turn time row `i` (0 to 3) | `(194, 337 + 27 * i, 110, 24)` | Always | On release inside, `planning_limit_choice` becomes `i` | FND-SETUP-013 |

## Keyboard input

None known.

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Push cue (slot 2) | `DATA/SND00202` | Add, Remove, Begin or Cancel is pressed, or a scenario, time limit, AI Mentality or turn time button | FND-AUDIO-010, FND-SETUP-013 |
| Accepted selection (slot 3) | `DATA/SND00203` | A selector arrow input is accepted | FND-AUDIO-010 |
| Rejected input (slot 4) | `DATA/SND00204` | A selector arrow input is refused, or Add or Remove cannot change the number of players (after the push cue) | FND-AUDIO-010 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Idle, with the roster of the last Begin in the session, or one local human in slot 0 with portrait 0 and card 0 selected the first time; `scenario` and `turn_limit` from RULE-SETUP-002 | The screen opens | A button or card is pressed | FND-RNG-005, FND-SETUP-005, FND-SETUP-013, SRC-HELP-GOG |
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
- The drawn cards start 12 pixels left of the input cells, which start at
  `(397,94)` (FND-SETUP-005), so the cell covers the portrait column and not the
  colour bar (FND-SETUP-014).
- Computer players get no card: the renderer restores the background for a
  slot whose `controller` is 1, as for an empty slot (FND-SETUP-014). What the
  background shows there has not been compared.
- The pressed images of the four push buttons are taken from
  `DATA/PX16/PX00140`; their source rectangles are not recorded.
- The keyboard handling of the screen is not recorded.
- In 256-colour mode the game uses the `DATA/PX08` files of the same names.
