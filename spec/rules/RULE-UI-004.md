---
id: RULE-UI-004
title: Drawing numbers in fixed glyph cells
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-006, FND-UI-004, FND-UI-023, FND-EXE-004, FND-UI-040, FND-UI-045, EXP-UI-002, EXP-UI-027, EXP-UI-028]
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
Item and Site Information, and the main console's year, remaining turns and
score (FND-UI-040).

## Parameters

None.

## Inputs

None.

## Procedure

```text
define number_cells(value, width, leading_zeros) -> INT32[]:
    let row = 0
    let v: INT32 = value
    if v < 0:
        row = 100
        # a 32-bit negate: -2147483648 stays negative
        v = -v
    let divisor = 1
    for k in 0..width - 1:
        divisor = divisor * 10
    let cells: INT32[] = []
    let started = leading_zeros
    for i in 0..width:
        # the first cell takes the whole quotient, which can exceed 9;
        # signed 32-bit division, truncating toward zero
        let q: INT32 = v / divisor
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
- A quotient above 42 addresses a glyph past `Z`, the strip's last character.
  The cell is still copied from source column `x`, at y 0, or y 8 when
  negative, where `x` is `6 * (16 + q)` cut to its low 16 bits and read as a
  signed number (FND-UI-045). While `0 <= x <= 506` the cell shows whatever art
  of `PX00129` lies there: quotients 0 to 68, and quotients whose product wraps
  back into that range, first 10907 to 10991, at columns 2 to 506.
- For any other `x` (a quotient of 69 or more outside those wrapped ranges, or
  a negative column) the source cell is wholly or partly outside the 512-by-646
  bitmap the sheet is held in. The copy then changes only the destination
  pixels whose source pixel lies inside the bitmap and leaves the others as
  they were: at column 510 the cell's two left pixel columns take the sheet's
  colour at columns 510 and 511, from either row, and a cell wholly outside,
  such as column 516, changes nothing (EXP-UI-002, EXP-UI-027, EXP-UI-028).
  The `StretchBlt` column 32766 left the black console black (EXP-UI-028).
  What a cell leaves unchanged keeps whatever was drawn there before, an
  earlier glyph included (EXP-UI-002). Since `6 * g` cut to 16 bits takes
  every even value, a cell partly overlaps the bitmap at four columns: 508
  and 510 on the right, with 4 and 2 pixel columns inside, and -4 and -2 on
  the left (glyphs 10922 and 21845), with 2 and 4. At -4 and -2 the inside
  part is sheet columns 0 and 1, or 0 to 3, within the space glyph's cell;
  at -4 it drew black (EXP-UI-027).
- -2147483648 stays negative when negated, so its quotients are negative: in
  two cells the first is -214748364, at column 13208, outside the bitmap, and
  the second -8, glyph 8, the character `(`, from the red row.

## What the sources say

None of the sources describes how numbers are drawn.

## Differences between builds

None known.

## Open questions

- Which panels use which helper for which field is listed in FND-UI-006 and in
  each screen entry.
- The runs are one system (EXP-UI-002, EXP-UI-027, EXP-UI-028). How GDI
  treats a source rectangle outside its bitmap may differ on other versions
  of Windows, which static reading of the game cannot settle (FND-UI-045).
- No run has drawn a cell at the `StretchBlt` columns 32762 and 32764, or at
  column 32766 over a cell that already held a glyph. The `StretchBlt` source
  width of -65530 spans the whole bitmap, and a capture over the black
  console cannot tell a copy that drew nothing at 32766 from one that drew
  black.
- No run has drawn a cell at column 508 or -2.
