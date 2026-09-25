---
id: RULE-COMLINK-006
title: Typing in Comlink Send overwrites a fixed grid of four rows of 40 upper-case characters
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-005, FND-COMLINK-007, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-005]
---

## Summary

The message is four lines of 40 characters. Letters are turned to capitals,
characters outside the game's set are ignored, and each character typed moves
the cursor on, into the next line after the 40th column. The arrow keys move
the cursor, Left and Right wrapping between lines, Enter moves to the start of
the next line, and Backspace steps back and blanks the character there.

## When it runs

In the Comlink Send panel, for each key event other than Execute.

## Parameters

- `virtual_key`: the Windows virtual-key code of the event, or 0.
- `character`: the character the event carries, or 0.

## Inputs

`comlink_draft`, `comlink_draft_row`, `comlink_draft_column`.

## Procedure

```text
let erase = false
if virtual_key == 0x08:
    erase = true
    comlink_draft_column = comlink_draft_column - 1
else if virtual_key == 0x0D:
    comlink_draft_column = 0
    comlink_draft_row = comlink_draft_row + 1
else if virtual_key == 0x25:
    comlink_draft_column = comlink_draft_column - 1
else if virtual_key == 0x26:
    comlink_draft_row = comlink_draft_row - 1
else if virtual_key == 0x27:
    comlink_draft_column = comlink_draft_column + 1
else if virtual_key == 0x28:
    comlink_draft_row = comlink_draft_row + 1
let c = character
if c >= 0x61 and c <= 0x7A:
    c = c - 0x20
if c >= 0x20 and c <= 0x5A:
    comlink_draft.text[comlink_draft_row * 40 + comlink_draft_column] = c
    comlink_draft_column = comlink_draft_column + 1
if comlink_draft_column < 0:
    comlink_draft_column = 39
    comlink_draft_row = comlink_draft_row - 1
if comlink_draft_column > 39:
    comlink_draft_column = 0
    comlink_draft_row = comlink_draft_row + 1
if comlink_draft_row < 0:
    comlink_draft_row = 0
if comlink_draft_row > 3:
    comlink_draft_row = 3
if erase:
    comlink_draft.text[comlink_draft_row * 40 + comlink_draft_column] = 0x20
```

## Outputs

No return value. Moves the text cursor, and writes one character or one space
into `comlink_draft.text`. The panel draws the changed cell and the caret
(SCR-COMLINK-002). Execute (`0x2B`) sends the message (RULE-COMLINK-003) and
is not handled here.

## Edge cases

- Lower-case letters `a` to `z` are stored as capitals. Any other character
  above `Z` (`0x5A`), such as `[` or `{`, is dropped, and so is any character
  below the space.
- Typing in column 39 moves the cursor to column 0 of the next row, and in
  column 39 of row 3 to column 0 of row 3, so further typing overwrites the
  start of the last row.
- Left and Backspace at column 0 go to column 39 of the row above; at row 0
  they go to column 39 of row 0, and Backspace blanks that cell. Right at
  column 39 goes to column 0 of the row below, and at row 3 to column 0 of
  row 3.
- Enter on row 3 moves to column 0 of row 3. Up and Down stop at rows 0 and 3.
- The cursor never leaves the grid.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says only that the player types
the message in the panel.

## Differences between builds

None known.

## Open questions

- Whether one key event can carry both a virtual key from the list and a
  printable character, which would apply both steps, depends on the event
  pump and was not traced (FND-COMLINK-007).
