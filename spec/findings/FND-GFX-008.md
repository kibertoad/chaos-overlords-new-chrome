---
id: FND-GFX-008
title: SetDIBits decodes every shipped PX08 file as the plain RLE8 reading does, clips runs at the line end, follows delta codes and leaves unwritten pixels as the bitmap held them
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: dynamic
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004273D5
  - build: BLD-GOG-EN-1.1
    file: DATA/PX08/PX00202
    offset: 0x436..0xCA9A
tool: a test of the rebuild calling gdi32 and a Python 3.14.7 ctypes script calling gdi32
environment: Windows 11 Pro 10.0.26200, gdi32 of that system, called in-process with no window and no device context
---

## Observation

On 2026-10-06 each shipped `PX08` file of BLD-GOG-EN-1.1 was uploaded with
`SetDIBits` the way the loader `0x004273D5` uploads it (FND-PLATFORM-002): the
file's information header and colour table with width and height replaced by
the size FND-GFX-003 gives and the plane count by 1, the file's bit count and
compression kept, colour-use value 0 (`DIB_RGB_COLORS`), into an 8-bit
bottom-up DIB section of that size whose pixels were 0. For every one of the
214 files, the 207 with `compression` 1 and the seven with 0, `SetDIBits`
reported every line set, and every pixel inside the file's documented width
and height equalled the value the plain BMP RLE8 reading of RULE-GFX-001 gives
it.

The same call was then made with hand-written RLE8 data on a 4-by-4 bitmap
whose pixels were first set to `0xAA`. Rows are listed from the bottom.

| Data | Result |
|---|---|
| `04 01 00 00 04 02 00 00 04 03 00 00 04 04 00 01` | `01 01 01 01`, `02 02 02 02`, `03 03 03 03`, `04 04 04 04` |
| `02 07 00 01` (end of bitmap after two pixels) | `07 07 AA AA`, every other pixel `AA` |
| `02 07` (no end code) | the same |
| `06 05 00 01` (encoded run of 6 on a 4-pixel line) | `05 05 05 05`, every other pixel `AA` |
| `00 05 01 02 03 04 05 00 00 01` (absolute run of 5) | `01 02 03 04`, every other pixel `AA` |
| `02 07 00 02 01 01 01 09 00 01` (delta 1 right, 1 up) | `07 07 AA AA`, `AA AA AA 09`, the rest `AA` |
| `01 06 00 00 01 08 00 01` (end of line) | `06 AA AA AA`, `08 AA AA AA`, the rest `AA` |

With the pixels first set to 0, the second row of the table gave `07 07 00 00`
and every other pixel 0. Each call reported four lines set.

## Interpretation

On this system `SetDIBits` decodes RLE8 as RULE-GFX-001's procedure reads: an
encoded or absolute run that passes the end of its line is cut there and the
rest of it is dropped, not carried onto the next line; a delta code moves the
position right and up without writing; the end-of-line code starts the next
line; and the data may end with or without the end-of-bitmap code. A pixel no
code writes keeps whatever the target bitmap held before the call, so it is 0
only when the target held 0.

## Alternatives

The original uploads into a temporary bitmap and then copies it to the
requested surface (FND-PLATFORM-002). How that bitmap is created, and so what
an unwritten pixel holds in the original, has not been read. No shipped file
leaves a pixel unwritten (FND-GFX-002, FND-GFX-003), so the shipped images do
not depend on it. Windows versions other than this one were not tried.

## How to reproduce

On Windows, upload each shipped `PX08` file with `SetDIBits` as the
Observation describes and compare every pixel inside the file's documented
width and height with the plain RLE8 reading of RULE-GFX-001. For the
hand-written cases, create a 4-by-4 8-bit
DIB section with `CreateDIBSection`, fill its pixels with `0xAA`, call
`SetDIBits` with a `BITMAPINFOHEADER` of width 4, height 4, 1 plane, 8 bits,
compression 1 (`BI_RLE8`) and the data's length as image size, a 256-entry
colour table and colour-use value 0, then `GdiFlush`, and read the pixels back.
