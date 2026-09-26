---
id: FND-GFX-003
title: The PX08 line geometry gives every image's width and height, and the PX16 rows are padded to four bytes
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/PX08/PX00202
    offset: 0x436..0xCA9A
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX00202
    offset: 0x36..0x3BE26
  - build: BLD-GOG-EN-1.1
    file: DATA/PX08/PX06008
    offset: 0x436..0xA162
  - build: BLD-GOG-EN-1.1
    file: DATA/PX16/PX06008
    offset: 0x36..0x1290A
tool: Python 3.14.7 script reading the file bytes
environment: null
---

## Observation

Every `PX08` file has a `PX16` file of the same name, ignoring case, and every
`PX16` file but `PX00131` has a `PX08` file. For each of the 214 pairs, the
width was taken as the pixel count of one decoded RLE8 line of the `PX08` file
and the height as its number of lines. For the seven uncompressed files, whose
71,896 pixel bytes carry no line structure, 344 by 209 was taken from the
compressed files of the same family and checked in the same way. In all 214 pairs the `PX16` pixel data is exactly
`height * ((width * 2 + 3) / 4 * 4)` bytes, where `/` truncates.

The sizes found:

| Files | Width x height |
|---|---|
| `PX00100`, `PX00128`, `PX00130`, `PX00143` to `PX00146` | 640 x 460 |
| `PX00129` | 512 x 646 |
| `PX00132` | 108 x 164 |
| `PX00137`, `PX00139` | 220 x 72 |
| `PX00138`, `PX04000` to `PX04052` | 720 x 48 |
| `PX00140` | 312 x 282 |
| `PX00150` | 220 x 56 |
| `PX00200` | 428 x 410 |
| `PX00201` | 320 x 240 |
| `PX00202`, `PX00203` | 311 x 393 |
| `PX00300` | 324 x 64 |
| `PX02000` | 120 x 1408 |
| `PX03000` | 640 x 576 |
| `PX04999` | 20 x 1280 |
| `PX05000` to `PX05024` (24 files) | 344 x 209 |
| `PX06001` to `PX06007`, `PX06009`, `PX06069` | 242 x 158 |
| `PX06008` | 241 x 157 |
| `PX07000` to `PX07027`, `PX07100` to `PX07119`, `PX07200` to `PX07228`, `PX07300` to `PX07320` | 512 x 64 |
| `PX10000` to `PX10006` | 432 x 416 |

`PX00131` has no `PX08` file. Its `PX16` pixel data is 588,800 bytes, the same
as the 640 by 460 images.

## Interpretation

Each `PX16` row is padded to a multiple of four bytes, as the BMP format
requires. Two widths are
odd: `PX00202` and `PX00203` are 311 pixels wide and `PX06008` is 241, so
their `PX16` rows end with two bytes of padding. `PX00131` exists only in the
16-bit set, so at 8-bit depth it cannot be loaded.

## Alternatives

The width and height the executable passes for each image (FND-PLATFORM-002)
have not been collected. If the executable passes 312 for `PX00202` or 242 for
`PX06008`, it reads the padding bytes as a last column of pixels.

## How to reproduce

Decode the RLE8 lines of each `PX08` file, count lines and pixels per line,
and compare with the pixel-data size of the `PX16` file of the same name.
