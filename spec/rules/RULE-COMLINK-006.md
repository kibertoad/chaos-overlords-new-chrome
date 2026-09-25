---
id: RULE-COMLINK-006
title: Typing in Comlink Send overwrites a fixed grid of four rows of 40 upper-case characters
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMLINK-005, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [FMT-STATE-005]
---

## Summary

The message is four lines of 40 characters. Letters are turned to capitals,
characters outside the game's set are ignored, the arrow keys move the cursor
inside the grid, and Enter moves to the start of the next line.

## When it runs

In the Comlink Send panel, for each key the player presses.

## Parameters

- `virtual_key`: the Windows virtual-key code of a key press, or 0 when the
  input is a character.
- `character`: the character typed, or 0 when the input is a key press.

## Inputs

`comlink_draft`, `comlink_draft_row`, `comlink_draft_column`.

## Procedure

```text
if virtual_key == 0x0D:
    comlink_draft_row = min(comlink_draft_row + 1, 3)
    comlink_draft_column = 0
else if virtual_key == 0x25:
    comlink_draft_column = max(comlink_draft_column - 1, 0)
else if virtual_key == 0x27:
    comlink_draft_column = min(comlink_draft_column + 1, 39)
else if virtual_key == 0x26:
    comlink_draft_row = max(comlink_draft_row - 1, 0)
else if virtual_key == 0x28:
    comlink_draft_row = min(comlink_draft_row + 1, 3)
else if virtual_key == 0 and character != 0:
    let c = character
    if c >= 0x61 and c <= 0x7A:
        c = c - 0x20
    if c >= 0x20 and c <= 0x5A:
        comlink_draft.text[comlink_draft_row * 40 + comlink_draft_column] = c
```

## Outputs

No return value. Moves the text cursor, or writes one character into
`comlink_draft.text` at the cursor. Backspace (`0x08`) deletes the character
at the cursor (see Open questions). Execute (`0x2B`) sends the message
(RULE-COMLINK-003) and is not handled here.

## Edge cases

- Lower-case letters are stored as capitals. Any other character above `Z`
  (`0x5A`), such as `[` or `{`, is dropped, and so is any character below
  the space.
- The cursor never leaves the grid.

## What the sources say

SRC-MANUAL-GOG, numbered page 23 (Comlink), says only that the player types
the message in the panel.

## Differences between builds

None known.

## Open questions

- Whether Enter on the last row stays on row 3 or does something else is not
  recorded; the procedure holds it at row 3 like the arrows.
- What Backspace stores at the cursor, and whether it moves the cursor, is not
  recorded, so it is left out of the procedure.
- Whether a typed character moves the cursor on, and where it goes after
  column 39, is not recorded.
- Whether the upper-case conversion is the C library's `toupper` or covers only
  `a` to `z` is not recorded; the two agree on the characters that are kept.
