---
id: FND-GFX-002
title: Every PX08 file is an 8-bit BMP with a 256-colour palette, RLE8 in 207 files and uncompressed in 7
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/PX08/Px00128
    offset: 0x00..0x436
  - build: BLD-GOG-EN-1.1
    file: DATA/PX08/PX05000
    offset: 0x00..0x436
  - build: BLD-GOG-EN-1.1
    file: DATA/PX08/PX07000
    offset: 0x436..0x366E
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

All 214 files under `DATA/PX08/` were read. In every one:

- the file starts with `BM`, the file-size word equals the file's size, and
  the reserved words are 0;
- the pixel-data offset at `0x0A` is 1,078 and the header size at `0x0E` is 40;
- the width and height are 0, the plane count is 255 and the bit count is 8;
- the image-size word at `0x22` equals the file size minus 1,078;
- the resolution words at `0x26` and `0x2A` are 2,835 and the colour counts are
  0;
- the 1,024 bytes from `0x36` to `0x436` are 256 four-byte palette entries
  whose fourth byte is 0.

The compression word at `0x1E` is 1 in 207 files and 0 in seven:
`PX05000`, `PX05001`, `PX05002`, `PX05004`, `PX05007`, `PX05018` and `Px05013`,
each 72,974 bytes. Their pixel data is 71,896 bytes.

In the 207 files with compression 1, the pixel data parses as BMP RLE8 from
`0x436` to the file's last byte: runs of a repeated byte, absolute runs padded
to an even length, end-of-line codes and one end-of-bitmap code as the last two
bytes. No file uses the delta code (`00 02`). Within each file every line
decodes to the same number of pixels.

## Interpretation

The `PX08` files are 8-bit Windows bitmaps with the same missing fields as the
`PX16` set (FND-GFX-001). Most are RLE8-compressed; the executable keeps the
file's compression when it uploads the pixels, so Windows decodes the RLE8
data (FND-PLATFORM-002). Because no stream uses the delta code, every pixel of
every line is written.

## Alternatives

None known.

## How to reproduce

Read the first 1,078 bytes of each file under `DATA/PX08/`, then walk the RLE8
codes from `0x436`.
