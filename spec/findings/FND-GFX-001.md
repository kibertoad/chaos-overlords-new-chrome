---
id: FND-GFX-001
title: Every PX16 file is a 16-bit BMP whose width and height are 0 and whose plane count is 255
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX00128
    offset: 0x00..0x36
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX07000
    offset: 0x00..0x36
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX00150
    offset: 0x00..0x36
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

All 215 files under `DATA/PX16/` were read. In every one:

- the file starts with `BM`, the file-size word at `0x02` equals the file's
  size, and the two reserved words at `0x06` and `0x08` are 0;
- the pixel-data offset at `0x0A` is 54 and the header size at `0x0E` is 40;
- the width at `0x12` and the height at `0x16` are 0;
- the plane count at `0x1A` is 255 and the bit count at `0x1C` is 16;
- the compression at `0x1E` is 0, and the image-size word at `0x22` equals the
  file size minus 54;
- the resolution words at `0x26` and `0x2A`, and the colour counts at `0x2E`
  and `0x32`, are 0.

The pixel data runs from `0x36` to the end of the file. No 16-bit pixel in any
file has bit 15 set. The file sizes fall into 19 groups, from 24,694 bytes
(`PX00150`) to 737,334 bytes (`PX03000`).

## Interpretation

The files are uncompressed 16-bit Windows bitmaps in which the width, height
and plane count were left out, which is why ordinary image viewers refuse
them. The executable writes its own values into those fields before it uses a
file (FND-PLATFORM-002). With bit 15 always clear, each pixel is a 15-bit
colour. FND-GFX-003 derives each file's width and height.

## Alternatives

Whether the pixels are RGB555 or RGB565 cannot be read from the header, since
compression 0 at 16 bits means RGB555 in the BMP definition but the game
could interpret the bits another way. Bit 15 being clear throughout, and a
comparison of all paired pixels with the `PX08` images, where RGB555 gives an
error about one sixth that of RGB565, favour RGB555.

## How to reproduce

Read the first 54 bytes of each file under `DATA/PX16/` as a BMP file header
and information header.
