---
id: FMT-GFX-001
title: 16-bit image files in DATA/PX16
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/PX16/*"]
byte_order: little
size: null
text: false
definition: fmt_gfx_001.ksy
evidence: [FND-GFX-001, FND-GFX-003, FND-GFX-005, FND-DATA-006, FND-PLATFORM-002, FND-PLATFORM-008, FND-ASSET-001]
conflicting: []
split_with: []
related: []
---

## Layout

Each file under `DATA/PX16/` is a Windows bitmap file whose width and height
fields hold 0 and whose plane count holds 255. The executable reads the file
as `data\PX16\PXnnnnn` when it runs at 16-bit depth, and writes its own width,
height and plane count over those fields before it uses the pixels
(FND-PLATFORM-002).

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 2 | `char[2]` | `signature` | `BM`, ASCII. | supported | FND-GFX-001 |
| `0x02` | 4 | `UINT32LE` | `file_size` | The file's size in bytes. | supported | FND-GFX-001 |
| `0x06` | 2 | `UINT16LE` | `reserved_06` | 0. | supported | FND-GFX-001 |
| `0x08` | 2 | `UINT16LE` | `reserved_08` | 0. | supported | FND-GFX-001 |
| `0x0A` | 4 | `UINT32LE` | `pixel_offset` | Offset of `pixels`, always 54. | supported | FND-GFX-001 |
| `0x0E` | 4 | `UINT32LE` | `header_size` | Size of the information header, always 40. | supported | FND-GFX-001 |
| `0x12` | 4 | `INT32LE` | `width` | 0 in every file. The executable replaces it with the width its caller gives. | supported | FND-GFX-001, FND-PLATFORM-002 |
| `0x16` | 4 | `INT32LE` | `height` | 0 in every file. The executable replaces it with the height its caller gives. | supported | FND-GFX-001, FND-PLATFORM-002 |
| `0x1A` | 2 | `UINT16LE` | `planes` | 255 in every file. The executable replaces it with 1. | supported | FND-GFX-001, FND-PLATFORM-002 |
| `0x1C` | 2 | `UINT16LE` | `bit_count` | Bits per pixel, always 16. | supported | FND-GFX-001, FND-PLATFORM-002 |
| `0x1E` | 4 | `UINT32LE` | `compression` | 0, uncompressed. | supported | FND-GFX-001 |
| `0x22` | 4 | `UINT32LE` | `image_size` | Size of `pixels` in bytes, the file size minus 54. | supported | FND-GFX-001 |
| `0x26` | 4 | `INT32LE` | `x_pixels_per_meter` | 0. | supported | FND-GFX-001 |
| `0x2A` | 4 | `INT32LE` | `y_pixels_per_meter` | 0. | supported | FND-GFX-001 |
| `0x2E` | 4 | `UINT32LE` | `colors_used` | 0; the file has no colour table. | supported | FND-GFX-001 |
| `0x32` | 4 | `UINT32LE` | `colors_important` | 0. | supported | FND-GFX-001 |
| `0x36` | `image_size` | `BYTE[image_size]` | `pixels` | `height` rows, the bottom row first. A row holds `width` pixels of 2 bytes, each a `UINT16LE` RGB555 colour (bits 0 to 4 blue, 5 to 9 green, 10 to 14 red, bit 15 always 0), then zero bytes up to a multiple of 4 bytes, so `image_size` is `height * ((width * 2 + 3) / 4 * 4)`. `width` and `height` are the values in Coverage. The colour `0x7FFF` is the key colour of the white-keyed copies. | supported | FND-GFX-001, FND-GFX-003, FND-PLATFORM-008 |
| | | | | Total size 54 + image_size | | |

## Enumerations and flags

None.

## Differences between builds

None known.

## Coverage

All 215 files under `DATA/PX16/` of BLD-GOG-EN-1.1 were read with a script
(FND-GFX-001): every header field holds the value in the table. Their widths
and heights, taken from the `PX08` files of the same names, fit their
`image_size` exactly with rows padded to four bytes (FND-GFX-003):

| Files | Width x height |
|---|---|
| `PX00100`, `PX00128`, `PX00130`, `PX00131`, `PX00143` to `PX00146` | 640 x 460 |
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
| `PX05000` to `PX05022`, `PX05024` | 344 x 209 |
| `PX06001` to `PX06007`, `PX06009`, `PX06069` | 242 x 158 |
| `PX06008` | 241 x 157 |
| `PX07000` to `PX07027`, `PX07100` to `PX07119`, `PX07200` to `PX07228`, `PX07300` to `PX07320` | 512 x 64 |
| `PX10000` to `PX10006` | 432 x 416 |

`PX00131` has no `PX08` file; its 588,800 pixel bytes are the size of the
other 640 x 460 images, and the executable passes 640 x 460 for it. The
executable passes these sizes for every image, except that it reads every
`PX06` image, `PX06008` included, as 242 x 158 (FND-GFX-005). The Kaitai
definition compiles and parses all 215 files (FND-DATA-006).

## Open questions

- `PX06008` is read one column and one row larger than its data: the last
  column is the row padding and the top row comes from memory past the end of
  the pixel block (FND-GFX-005). What that row shows has not been observed.
- RGB555 rests on bit 15 being clear in every pixel and on a comparison with
  the `PX08` images (FND-GFX-001), not on a read of how the executable treats
  the channels.
