---
id: RULE-UI-004
title: Drawing numbers in fixed glyph cells
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-006, FND-UI-004]
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
    let cells: INT32[] = []
    for k in 0..width:
        append(cells, -1)
    let row = 0
    let v = value
    if v < 0:
        row = 10
        v = -v
    let i = width - 1
    while i >= 0:
        if v != 0 or i == width - 1 or leading_zeros != 0:
            cells[i] = row + v % 10
        v = v / 10
        i = i - 1
    return cells

define modifier_cells(value, width) -> INT32[]:
    if value != 0:
        return number_cells(value, width, 0)
    let cells: INT32[] = []
    for k in 0..width - 1:
        append(cells, -1)
    append(cells, 20)
    return cells
```

## Outputs

Both functions return one code per cell, from left to right: -1 for a blank
cell, 0 to 9 for that digit in bright green from the font strip of `PX00129`
(`(96 + 6*d, 0, 6, 7)` for digit `d`, the strip holding one cell per ASCII character from space
up), 10 to 19 for digit `code - 10` from the red row at source y 8, and 20 for
the dim green 0 at `(354,8,6,7)`. `number_cells` is the baseline helper, and
every panel passes `leading_zeros` 0. `modifier_cells` is the modifier helper.

## Edge cases

- -2 in two cells is a blank and a red 2; -12 is a red 1 and a red 2.
- A value with more digits than cells keeps only its lowest digits, if the
  helpers walk from the right as written (see Open questions).

## What the sources say

None of the sources describes how numbers are drawn.

## Differences between builds

None known.

## Open questions

- The direction in which the helpers walk the cells, and so what a value too
  wide for its cells shows.
- The source x of the red digits is taken to match the bright digits' columns;
  it has not been recorded.
- Which panels use which helper for which field is listed in FND-UI-006 and in
  each screen entry.
