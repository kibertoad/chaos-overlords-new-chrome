---
id: SCR-COMLINK-002
title: Comlink Send panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
resolution: 640x480
evidence: [FND-COMLINK-001, FND-COMLINK-002, FND-COMLINK-003, FND-COMLINK-005, FND-AUDIO-002, FND-AUDIO-011, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-COMLINK-002, RULE-COMLINK-003, RULE-COMLINK-006]
---

## Drawn elements

| Element | Resource | Shows | Position | Shown when | Evidence |
|---|---|---|---|---|---|
| Panel | `DATA/PX16/PX05018` | None | (104, 124, 344, 209) | While the panel is open | FND-COMLINK-001, FND-COMLINK-003 |
| Player card, one per player slot | Name and portrait of the player; resources not recorded | None | 105 by 34, around each recipient target below; exact position not recorded | Always, for all six slots, including the active player, computer players and empty slots | FND-COMLINK-003 |
| Card backing | None | Black, or green while `comlink_selected` is set for the slot | Behind each card | Always | FND-COMLINK-003 |
| Dimmed card | Not recorded | None | Over the card | `comlink_eligible` is clear for the slot | FND-COMLINK-003 |
| Message characters | 6-by-7 cells from the plain character row of `DATA/PX16/PX00129` at y 0 | `comlink_draft.text` | (199 + 6 * column, 256 + 8 * row, 6, 7) for rows 0 to 3 and columns 0 to 39 | Always | FND-COMLINK-005 |
| Caret | The same cell from the inverse character row of `DATA/PX16/PX00129` at y 441 | The character at the text cursor | The cell at (`comlink_draft_row`, `comlink_draft_column`) | During the inverse phase of the caret (Timing) | FND-COMLINK-005 |
| Cancel pressed | `DATA/PX16/PX00129` rectangle (50, 409, 50, 23) | None | (137, 261, 50, 23) | While Cancel is held with the pointer inside it | FND-COMLINK-003 |
| Send pressed | `DATA/PX16/PX00129` rectangle (50, 386, 50, 23) | None | (137, 293, 50, 23) | While Send is held with the pointer inside it | FND-COMLINK-003 |

## Mouse input

| Region | Rectangle | Enabled when | Effect | Evidence |
|---|---|---|---|---|
| Recipient cell 1 | (202, 144, 100, 32) | Always | Flips `comlink_selected` for the slot at once when `comlink_eligible` is set; otherwise rejects | FND-COMLINK-003 |
| Recipient cell 2 | (202, 178, 100, 32) | Always | As recipient cell 1 | FND-COMLINK-003 |
| Recipient cell 3 | (202, 212, 100, 32) | Always | As recipient cell 1 | FND-COMLINK-003 |
| Recipient cell 4 | (323, 144, 100, 32) | Always | As recipient cell 1 | FND-COMLINK-003 |
| Recipient cell 5 | (323, 178, 100, 32) | Always | As recipient cell 1 | FND-COMLINK-003 |
| Recipient cell 6 | (323, 212, 100, 32) | Always | As recipient cell 1 | FND-COMLINK-003 |
| Cancel | (137, 261, 49, 22) | Always | On release inside, closes the panel without sending | FND-COMLINK-003 |
| Send | (137, 293, 49, 22) | Always | With no recipient selected, rejects before the button is pressed; otherwise, on release inside, sends through RULE-COMLINK-003 | FND-COMLINK-003 |

## Keyboard input

| Key | Enabled when | Effect | Evidence |
|---|---|---|---|
| Execute (virtual key `0x2B`) | Always | Sends through RULE-COMLINK-003; rejected with no recipient selected | FND-COMLINK-003 |
| Enter | Always | Moves the text cursor to column 0 of the next row (RULE-COMLINK-006) | FND-COMLINK-005 |
| Backspace | Always | Deletes the character at the text cursor (RULE-COMLINK-006) | FND-COMLINK-005 |
| Left, Up, Right, Down | Always | Moves the text cursor, held inside four rows and 40 columns (RULE-COMLINK-006) | FND-COMLINK-005 |
| Any key typing a character | Always | Writes the character, turned to upper case, at the text cursor when it lies from space to `Z` (RULE-COMLINK-006) | FND-COMLINK-005 |

## Other input

None.

## Sounds

| Sound | Resource | Played when | Evidence |
|---|---|---|---|
| Accepted input | `DATA/SND00203` | Cancel or Send is pressed | FND-COMLINK-003, FND-AUDIO-011 |
| Rejected input | `DATA/SND00204` | A click on the card of a slot that is not eligible; Send or Execute with no recipient selected; the Send half of the Comlink control is pressed when no other player is eligible, and the panel then does not open (RULE-COMLINK-002) | FND-COMLINK-003, FND-AUDIO-011 |

## States

| State | Entered when | Left when | Evidence |
|---|---|---|---|
| Composing | RULE-COMLINK-002 opens the panel | Cancel or Send is released inside itself, or Execute sends | FND-COMLINK-003 |
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

- Which player slot each recipient cell belongs to is not recorded; the table
  numbers them down the first column, then down the second.
- The findings describe the recipient and button rectangles as panel-local
  without restating the panel origin; the rectangles use the standard panel
  origin (104, 124) of FND-COMLINK-002. The character cells are given as the
  finding states them, which places them inside the panel on the screen.
- The exact position of the 50-by-23 pressed faces, one pixel larger than the
  49-by-22 controls, is given from the controls' corner and still needs
  checking.
- The alternative template, resource 5023, has no file in this build's
  `DATA/PX16/` or `DATA/PX08/` directories; when the game would ask for it is
  not recorded.
- Where in `DATA/PX16/PX00129` each character's cell lies is not recorded.
- Whether Send closes the panel is not recorded.
