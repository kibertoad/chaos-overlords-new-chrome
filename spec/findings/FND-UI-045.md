---
id: FND-UI-045
title: The number helpers copy each glyph cell with a GDI BitBlt from the 512-by-646 sheet surface, at a source column cut to 16 bits
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004141FE..0x0041427E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004142E7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425EDF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042773E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425FB0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462420..0x0046243B
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004.

- In `fn_00414187` each cell divides the remaining value by the cell's power
  of ten with a signed 32-bit divide (`0x00414202`) and keeps the quotient as a
  32-bit number. A drawn cell adds 16 to it (`0x00414233`). The source column
  is that glyph number times 6, computed in 32 bits (`0x00414259..0x00414261`
  and `0x0041426C..0x00414274`), pushed with the source row (0, or 8 for a
  negative value) to the rectangle packer `fn_00425EDF`, together with the
  column plus 6 and the row plus 7.
- `fn_00425EDF` packs its four arguments as 16-bit numbers, in the order top,
  left, bottom, right, so only the low 16 bits of each column survive.
  `fn_004142E7` computes its cells the same way, with the glyph number held in
  a 16-bit local.
- The value is negated with a 32-bit negate when it is below 0. For
  -2147483648 the result is the same negative number, so every quotient and
  remainder is negative or 0: in two cells the first quotient is -214748364 and
  the second -8.
- The copy `fn_0042773E(6, destination, source, destination rectangle)` reads
  each edge of both rectangles as a signed 16-bit number. When the source and
  destination widths and heights are equal it calls `BitBlt` with `SRCCOPY`
  (`0xCC0020`) from the memory DC of surface 6 at the source's left and top;
  otherwise it calls `StretchBlt` with the stretch mode 3 and the source width
  and height taken from the rectangle. The width of a source cell is
  `(int16)(column + 6) - (int16)column`, which is 6 except when the column's
  low 16 bits are 32762, 32764 or 32766, where the right edge wraps to a
  negative number and the copy goes to `StretchBlt` with a source width of
  -65530.
- Surface 6 is created at `0x00462436` by `fn_00425FB0(6, 2, 0x200, 0x286, ...)`:
  a memory DC with `CreateCompatibleBitmap` of 512 by 646 pixels, filled by
  `PatBlt` with `PATCOPY` (`0xF00021`) while the stock white brush is selected. `PX00129` is then
  loaded into it with `fn_00464108(6, 0x81, ...)` (FND-UI-031).
- Neither helper nor the copy tests the source rectangle against the size of
  the bitmap.

## Interpretation

The source column of a cell with glyph number `g = 16 + q` is `x = int16(6 *
g)`, the product taken modulo 65536 and read as signed. While `0 <= x <= 506`
the whole 6-by-7 cell lies inside the 512-pixel-wide bitmap and the copy shows
the sheet's pixels there, so the drawn cell is settled by this reading: the
glyphs 16 to 84 (quotients 0 to 68), and the glyphs whose product wraps back
into that range, first 10923 to 11007 (quotients 10907 to 10991), which start
at columns 2 to 506 and so are not aligned with the sheet's 6-pixel cells.

Everywhere else part or all of the source cell lies outside the bitmap: columns
508 and 510 hold 4 and 2 of its 6 pixel columns, columns from 512 to 32766 and
all negative columns hold none. What `BitBlt` and `StretchBlt` copy from
outside a memory DC's bitmap is decided by GDI, not by the game, and this
reading cannot say whether the destination keeps its pixels, is filled, or the
call fails.

The quotient -214748364 of -2147483648 gives the glyph number -214748348, at
column 13208, outside the bitmap; the remainder -8 gives glyph 8, the
character `(` of the font strip, from the red row.

## Alternatives

None known.

## How to reproduce

In `0x00414187`, find the `IDIV` at `0x00414202`, the `ADD` of `0x10` at
`0x00414233` and the two `LEA EAX,[EAX + EAX*0x2]; ADD EAX,EAX` sequences
before the call of `0x00425EDF`. In `0x0042773E`, find the width compare and
the calls of `BitBlt` and `StretchBlt`. In `0x004622D4`, find the call of
`0x00425FB0` at `0x00462436` with the pushes `0x6`, `0x2`, `0x200` and `0x286`,
and in `0x00425FB0` the calls of `CreateCompatibleBitmap` and `PatBlt`.
