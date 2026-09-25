---
id: RULE-UI-004
title: Drawing numbers in fixed glyph cells
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-006, FND-UI-004, FND-UI-023, FND-EXE-004]
conflicting: []
split_with: []
related: []
---

## Summary

Panels draw numbers into a fixed number of 6-by-7 cells, right-aligned. A
negative number is drawn in red without a minus sign. Base values show 0 as a
bright green 0; modifiers show 0 as a dim green 0.

## When it runs

Whenever a panel draws a number: Gang, Gang Definition, Hire, Gangs in Sector,
Item and Site Information.

## Parameters

None.

## Inputs

None.

## Procedure

```text
define number_cells(value, width, leading_zeros) -> INT32[]:
    let row = 0
    let v = value
    if v < 0:
        row = 100
        v = -v
    let divisor = 1
    for k in 0..width - 1:
        divisor = divisor * 10
    let cells: INT32[] = []
    let started = leading_zeros
    for i in 0..width:
        # the first cell takes the whole quotient, which can exceed 9
        let q = v / divisor
        v = v % divisor
        if q != 0 or i == width - 1:
            started = 1
        if started != 0:
            append(cells, row + q)
        else:
            append(cells, -1)
        divisor = divisor / 10
    return cells

define modifier_cells(value, width) -> INT32[]:
    if value != 0:
        return number_cells(value, width, 0)
    let cells: INT32[] = []
    for k in 0..width - 1:
        append(cells, -1)
    append(cells, 200)
    return cells
```

## Outputs

Both functions return one code per cell, from left to right: -1 for a blank
cell, drawn with the space glyph at `(0,0,6,7)`; a code `q` below 100 for the
glyph `16 + q` in bright green from the font strip of `PX00129`
(`(96 + 6*q, 0, 6, 7)`, the strip holding one cell per ASCII character from
space up), so 0 to 9 are the digits; `100 + q` for the same glyph from the red
row at source y 8; and 200 for the dim green 0 at `(354,8,6,7)`. The cells are
filled from the left. `number_cells` is the baseline helper, and
every panel passes `leading_zeros` 0. `modifier_cells` is the modifier helper.

## Edge cases

- -2 in two cells is a blank and a red 2; -12 is a red 1 and a red 2.
- A value with more digits than cells puts its whole leading quotient in the
  first cell: 123 in two cells draws glyph 28, the character `<`, and then 3.
  The strip's glyph after `9` is drawn, so the value shows a punctuation mark.

## What the sources say

None of the sources describes how numbers are drawn.

## Differences between builds

None known.

## Open questions

- Which panels use which helper for which field is listed in FND-UI-006 and in
  each screen entry.
