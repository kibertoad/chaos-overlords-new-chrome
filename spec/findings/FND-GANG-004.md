---
id: FND-GANG-004
title: Gang panels draw each number in a fixed field of two glyph cells that replaces the template's placeholder
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449E80
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00455B6B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004142E7
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The live-gang panel handler `fn_00449E80`, for `DATA/PX08/PX05000`, draws its
  numbers, including every statistic row, with the two-character integer
  helpers at buffer x = 172 and x = 268 (screen x = 276 and x = 372). It
  negates the stored Upkeep before drawing it. When the panel shows no gang
  instance, its Force branch draws the two-character unknown marker in the same
  two-cell field.
- Every numeric call from `fn_00449E80` and `fn_00455B6B` to `fn_004142E7`
  passes a width of 2 and turns leading zeros off. For a one-digit value the
  first step copies an opaque blank glyph over the first cell and the second
  copies the digit. A negative value selects the red row of digits and draws
  the digits of its absolute value, with no minus glyph and no third cell.
- The decoded `PX05022` image holds two green `0` glyphs at every numeric
  field before the handler draws over them. It holds no multiplier and no
  literal `00` suffix.

## Interpretation

Each number on the gang panels fills exactly two glyph cells starting at the
field's origin, and the blank cell of a one-digit value hides the template's
first `0`. A stored value of 3 is drawn as a blank and a 3; it is never
scaled to 300. A visible `00` after a number means the field was drawn at the
wrong origin.

## Alternatives

The screen position of `PX05000` and its buffer-to-screen offset are not
recorded in this finding; the screen x values above are the recorded mapping.
Which of the `PX08` and `PX16` copies of `PX05022` was decoded, and the glyph
positions inside the file, have not been recorded.

## How to reproduce

Start at `fn_004142E7` and list its callers inside `fn_00449E80` and
`fn_00455B6B`; each passes 2 as the width. Decode `DATA/PX08/PX05022` and look at
the numeric fields listed in FND-GANG-002.
