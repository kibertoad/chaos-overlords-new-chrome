---
id: FND-UI-019
title: Text, numbers and the panel buttons are drawn from fixed cells of PX00129 by four small helpers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00413FD5..0x004140AD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004140AE..0x00414186
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041B668..0x0041B7D5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418CCC..0x00418E65
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418E66..0x00418F15
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges and caller counts are those of FND-EXE-004.
Rectangles are half-open corners `(left,top)-(right,bottom)` of the sheet
`PX00129` (surface 6), copied opaquely with `fn_0042773E`.

- `fn_00413FD5(surface, point, text)` (34 callers) draws a NUL-terminated
  string on the given surface. Character `ch` is the 6-by-7 cell
  `(6 * (ch - 32), 0)-(6 * (ch - 32) + 6, 7)`; each character moves the pen 6
  pixels to the right. There is no clipping, kerning or line break.
- `fn_004140AE(surface, point, text)` is the same routine reading the second
  font row, `(6 * (ch - 32), 8)-(... , 15)`. Nothing calls it.
- `fn_0041B668(surface, point, value, digits, leading)` draws `value` in a
  field of `digits` six-pixel cells from the font at `(152,274)`: digit `d` is
  the cell `(152 + 6 * (16 + d), 274)`, 6 by 7. A negative value is drawn as
  its magnitude from the row 8 pixels lower, `(..., 282)`. Leading zeros are
  drawn as the blank cell at `(152,274)` unless `leading` is set, and the last
  digit is always drawn. Its only caller is the gang panel `fn_00449E80`, 14
  times.
- `fn_00418CCC(kind, rect)` plays effect slot 3, copies a pressed image to the
  window at `rect`, waits one tick with `fn_00464CD9(1)`, and copies the
  released image. Kind 0 uses pressed `(0,386)-(50,409)` and released
  `(50,386)-(100,409)`; kind 1 uses `(0,409)-(50,432)` and
  `(50,409)-(100,432)`; kind 3 uses `(120,205)-(152,268)` and
  `(460,211)-(492,274)`. It has 24 callers in the panels and the sector view.
- `fn_00418E66(rect, enabled)` copies `(50,386)-(100,409)` to the window when
  `enabled` is nonzero and `(100,386)-(150,409)` otherwise. It is called from
  eight panel handlers: Attack `fn_0043B290`, Equip `fn_0043DAD9`, Influence
  `fn_0043F692`, Move `fn_004413EF`, Research `fn_004427FA`, Sell `fn_00443BBD`,
  Give `fn_00445A4F` and Comlink `fn_0045EAB1`.

## Interpretation

The whole interface uses one fixed-width 6-by-7 font from `PX00129`, and
numbers in the gang panel use a second copy of the digits in two colours, one
for negative values. The 50-by-23 panel button has three states: pressed,
released, and a third image at `(100,386)` that `fn_00418E66` shows when a
panel's button is not available. Kind 3 of `fn_00418CCC` is the back control of
the sector view (FND-UI-015).

## Alternatives

- The colours of the two digit rows at y 274 and y 282 have not been read from
  the sheet.
- Whether the image at `(100,386)` is a greyed button has not been checked in
  the art.

## How to reproduce

In `0x00413FD5` and `0x004140AE`, find the subtraction of 32 and the multiply
by 6, and the source rows 0 and 8. In `0x0041B668`, find the constants 152,
274, 8 and 16 and the division by 10. In `0x00418CCC`, find the call to
`0x00464290` with 3, the rectangles above and the call to `0x00464CD9`. In
`0x00418E66`, find the constants 50, 100, 150 and 386.
