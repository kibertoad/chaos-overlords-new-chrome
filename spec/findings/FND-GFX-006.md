---
id: FND-GFX-006
title: A pattern fill takes its bitmap from the high byte of a 16-bit grey, starts the pattern at the filled rectangle's corner, and outlines the fill with the scratch surface's own pen
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00425E99..0x00425EDE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449B20..0x00449B77
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004266A6..0x00426908
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00427E60..0x004282A9
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges are those of FND-EXE-004. Rectangles are written
`(left,top)-(right,bottom)`, half-open.

- `fn_00425E99(out, r, g, b)` stores its three arguments as three 16-bit
  fields, each shifted right by 8. Every colour the drawing helpers take is
  built this way, so a caller's 16-bit component `v` becomes `v >> 8`: 0x7FFF
  gives 127, 48,000 gives 187, 0x4000 gives 64 and 0xFFFF gives 255.
- `fn_00449B20(colour)` reads the first field with `MOVSX` (`0x00449B29`) and
  compares it with 0x55 (`0x00449B2C`) and 0xAA (`0x00449B4A`) as signed
  values: up to 85 it stores 2 at `0x00494868`, from 86 to 170 it stores 0,
  above 170 it stores 1. `fn_00427E60` loads bitmap resource 147 for 2, 143
  for 0 and 146 for 1 (FND-GFX-004, FND-UI-031). So 0x7FFF selects 143, 48,000
  selects 146 and 0x4000 selects 147.
- The three bitmaps are 8 by 8, one bit deep, with palette entry 0 black and
  entry 1 white. Read from the top, 143 alternates rows `0x55` and `0xAA`, 146
  alternates `0x88` and `0x22`, and 147 alternates `0xDD` and `0x77`, as
  FND-UI-031 records.
- `fn_004266A6(rect, colour, fill, shade)` with `fill` set and `shade` 0 to 2
  creates a pen of the colour and selects it into the target surface's device
  context, creates a solid brush of the colour and selects it into the device
  context of slot 11 (`0x00493844`), and calls `Rectangle(0, 0, w, h)` there,
  `w` and `h` being the rectangle's width and height. The only reads of
  `0x00493844` are the three in this function (`0x00426767`, `0x00426784`,
  `0x00426796`), and none selects a pen, so the outline of that `Rectangle`
  is drawn with whatever pen slot 11's device context holds. It then calls
  `fn_00427E60(11, target, (0,0)-(w,h), corner)`.
- `fn_00427E60(source, target, source_rect, corner)` creates two monochrome
  bitmaps and two bitmaps compatible with the target, each `w` by `h`. It
  copies the source area into the first colour bitmap, fills the first
  monochrome bitmap from `(0,0)` with a pattern brush of the chosen bitmap
  (`PatBlt` with `0xF00021`, PATCOPY), copies it inverted into the second
  monochrome bitmap (`0x330008`, NOTSRCCOPY), copies the target area at
  `corner` into the second colour bitmap, combines that with the pattern
  (`0x8800C6`, SRCAND), combines the source copy with the inverted pattern
  (`0x8800C6`), merges the two (`0x660046`, SRCINVERT), and copies the result
  to the target at `corner` (`0xCC0020`, SRCCOPY).

## Interpretation

A pattern fill replaces a pixel of the target with the fill where the
pattern's bit is clear and keeps the target where it is set, as FND-UI-031
says. The pattern is laid from the filled rectangle's own top-left corner,
because the brush is applied to a bitmap of the rectangle's size at `(0,0)`;
the target's coordinates play no part. Bitmap 143 replaces half of the pixels,
146 replaces 48 of every 64, and 147 replaces 16 of every 64.

The fill's outermost pixels come from the pen of slot 11's device context and
the inside from the brush. A device context starts with the stock black pen,
and nothing in the executable changes slot 11's, so the border pixels the
pattern replaces are black whatever the fill colour. For a black fill this
makes no difference; for a white fill the replaced border pixels are black.

The setup renderer's grey 0x4000 (FND-SETUP-014) selects bitmap 147; that
finding read it as selecting 1.

## Alternatives

- The outline colour rests on Windows giving a new memory device context the
  black pen; the executable does not set it.
- At 8-bit depth the raster operations work on palette indices. The mask
  steps give the intended result only when black and white map to indices 0
  and 255 in slot 11 and the target; that has not been checked.

## How to reproduce

In `0x00425E99`, find the three shifts right by 8. In `0x00449B20`, find the
`MOVSX` and the compares with `0x55` and `0xAA`. In `0x004266A6`, find the
pen selected into the target and the brush selected into the device context
at `0x00493844` before `Rectangle(0,0,w,h)` and the call to `0x00427E60` with
11. In `0x00427E60`, find the `LoadBitmapA` of `0x8F`, `0x92` and `0x93`,
`CreatePatternBrush`, the `PatBlt` with `0xF00021` at `(0,0)` and the
`BitBlt` raster operations `0x330008`, `0xCC0020`, `0x8800C6` and `0x660046`.
Extract `BITMAP/143`, `BITMAP/146` and `BITMAP/147` and read their rows and
palettes.
