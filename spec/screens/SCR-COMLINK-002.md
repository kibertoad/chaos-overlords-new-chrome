---
id: SCR-COMLINK-002
title: Comlink Send panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-COMLINK-001, FND-COMLINK-002, FND-COMLINK-003, FND-COMLINK-005, FND-COMLINK-006, FND-COMLINK-007, FND-AUDIO-002, FND-AUDIO-011, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-COMLINK-002, RULE-COMLINK-003, RULE-COMLINK-006]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05018` | None | (104, 124, 344, 209) | While the panel is open | FND-COMLINK-001, FND-COMLINK-003 |
| Player card, one per player slot | See the next rows | None | Slot `p` at the card origin (202 + 121 * (p / 3), 144 + 34 * (p % 3)): slots 0 to 2 down the left column, 3 to 5 down the right | Always, for all six slots, including the active player, computer players and empty slots | FND-COMLINK-003, FND-COMLINK-007 |
| Card colour | Fill in the player's colour, each component divided by 4 when `comlink_eligible` is clear | None | (card x + 1, card y + 1, 7, 30) | Always | FND-COMLINK-007 |
| Card portrait | `DATA/PX16/PX00129` rectangle (`portrait * 32`, 480, 32, 32), or (`portrait * 32`, 594, 32, 32) when `comlink_eligible` is clear | None | (card x + 8, card y, 32, 32) | Always | FND-COMLINK-007 |
| Card name | Character cells of `DATA/PX16/PX00129` from the row at y 0, or from the row at y 274 when `comlink_eligible` is clear | The name in `player_names` | (card x + 42, card y + 2) | Always | FND-COMLINK-007 |
| Card frame | A one-pixel outline | Black, or green while `comlink_selected` is set for the slot | (card x - 1, card y - 1, 105, 34) | Always | FND-COMLINK-003, FND-COMLINK-007 |
| Message characters | 6-by-7 cells from the plain character row of `DATA/PX16/PX00129` at y 0, character `c` at x `(c - 0x20) * 6` | `comlink_draft.text` | (199 + 6 * column, 256 + 8 * row, 6, 7) for rows 0 to 3 and columns 0 to 39 | Always | FND-COMLINK-005 |
| Caret | The same cell from the inverse character row of `DATA/PX16/PX00129` at y 441 | The character at the text cursor | The cell at (`comlink_draft_row`, `comlink_draft_column`) | During the inverse phase of the caret (Timing) | FND-COMLINK-005 |
| Cancel pressed | `DATA/PX16/PX00129` rectangle (50, 409, 50, 23) | None | (137, 261, 50, 23) | While Cancel is held with the pointer inside it | FND-COMLINK-003 |
| Send pressed | `DATA/PX16/PX00129` rectangle (50, 386, 50, 23) | None | (137, 293, 50, 23) | While Send is held with the pointer inside it | FND-COMLINK-003 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Outside the panel | Anything outside (104, 124, 344, 209) | Always | A press or double-click plays the rejected-input sound | FND-COMLINK-007 |
| Recipient cell of slot 0 | (202, 144, 100, 32) | Always | On a press, flips `comlink_selected` for the slot between 0 and 1 when `comlink_eligible` is set, and redraws the cards and the Send face; otherwise rejects | FND-COMLINK-003, FND-COMLINK-007 |
| Recipient cell of slot 1 | (202, 178, 100, 32) | Always | As the cell of slot 0 | FND-COMLINK-003 |
| Recipient cell of slot 2 | (202, 212, 100, 32) | Always | As the cell of slot 0 | FND-COMLINK-003 |
| Recipient cell of slot 3 | (323, 144, 100, 32) | Always | As the cell of slot 0 | FND-COMLINK-003 |
| Recipient cell of slot 4 | (323, 178, 100, 32) | Always | As the cell of slot 0 | FND-COMLINK-003 |
| Recipient cell of slot 5 | (323, 212, 100, 32) | Always | As the cell of slot 0 | FND-COMLINK-003 |
| Cancel | (137, 261, 49, 22) | Always | On release inside, closes the panel without sending | FND-COMLINK-003 |
| Send | (137, 293, 49, 22) | Always | With no recipient selected, rejects before the button is pressed; otherwise, on release inside, closes the panel and sends through RULE-COMLINK-003 | FND-COMLINK-003, FND-COMLINK-007 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Execute (virtual key `0x2B`) | Always | Sends through RULE-COMLINK-003; rejected with no recipient selected | FND-COMLINK-003 |
| Enter | Always | Moves the text cursor to column 0 of the next row (RULE-COMLINK-006) | FND-COMLINK-005 |
| Backspace | Always | Moves the text cursor back one cell, wrapping to the row above, and blanks that cell (RULE-COMLINK-006) | FND-COMLINK-005, FND-COMLINK-007 |
| Left, Up, Right, Down | Always | Moves the text cursor; Left and Right wrap between rows, and the rows are held between 0 and 3 (RULE-COMLINK-006) | FND-COMLINK-005, FND-COMLINK-007 |
| Any key typing a character | Always | Writes the character, with `a` to `z` turned to capitals, at the text cursor when it lies from space to `Z`, and moves the cursor on (RULE-COMLINK-006) | FND-COMLINK-005, FND-COMLINK-007 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` | Cancel or Send is pressed | FND-COMLINK-003, FND-AUDIO-011 |
| Rejected input | `DATA/SND00204` | A press outside the panel; a click on the card of a slot that is not eligible; Send or Execute with no recipient selected; the Send half of the Comlink control is pressed when no other player is eligible, and the panel then does not open (RULE-COMLINK-002) | FND-COMLINK-003, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Composing | RULE-COMLINK-002 opens the panel, with no recipient selected, a blank draft and the cursor at row 0, column 0 | Cancel or Send is released inside itself, or Execute is pressed with a recipient selected | FND-COMLINK-003, FND-COMLINK-007 |
| Button held | Cancel or Send is pressed | The button is released; leaving the button while holding it puts its plain face back, and releasing outside does nothing | FND-COMLINK-003 |
| Caret plain, caret inverse | The panel opens (plain), then every third timer event switches between them | The panel closes | FND-COMLINK-005 |

## Timing

The caret uses timer 0, which the game registers with `timeSetEvent` at a
period of `1000 / 6` = 166 milliseconds. The Send loop raises the timer flag
itself when it starts, then counts the timer events it consumes, and every
third one switches the caret between the plain and the inverse character row.
Each phase therefore lasts three events, 498 milliseconds, and the caret starts
plain [FND-COMLINK-005].

## Differences between builds

None known.

## Open questions

- The Send face drawn by `fn_00418E66` after each card click presumably shows
  Send dimmed while no recipient is selected; the faces it copies were not
  read.
- Resource 5023, which has no file in this build, is never loaded: the flag
  that would select it is 0 and nothing writes it (FND-COMLINK-007).
