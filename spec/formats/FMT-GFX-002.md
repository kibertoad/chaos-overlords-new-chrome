---
id: FMT-GFX-002
title: 8-bit image files in DATA/PX08
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/PX08/*"]
byte_order: little
size: null
text: false
definition: fmt_gfx_002.ksy
evidence: [FND-GFX-002, FND-GFX-003, FND-GFX-005, FND-PLATFORM-011, FND-DATA-006, FND-PLATFORM-002, FND-ASSET-001]
conflicting: []
split_with: []
related: [RULE-GFX-001]
---

## Layout

Each file under `DATA/PX08/` is an 8-bit Windows bitmap file whose width and
height fields hold 0 and whose plane count holds 255. The executable reads it
as `data\PX08\PXnnnnn` when it runs at 8-bit depth, writes its own width,
height and plane count over those fields, and hands the pixel data to Windows
with the file's compression unchanged (FND-PLATFORM-002). One entry covers
both variants: the pixel data is RLE8-compressed when `compression` is 1 and
stored as plain rows when it is 0.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 2 | `char[2]` | `signature` | `BM`, ASCII. | supported | FND-GFX-002 |
| `0x02` | 4 | `UINT32LE` | `file_size` | The file's size in bytes. | supported | FND-GFX-002 |
| `0x06` | 2 | `UINT16LE` | `reserved_06` | 0. | supported | FND-GFX-002 |
| `0x08` | 2 | `UINT16LE` | `reserved_08` | 0. | supported | FND-GFX-002 |
| `0x0A` | 4 | `UINT32LE` | `pixel_offset` | Offset of the pixel data, always 1,078. | supported | FND-GFX-002 |
| `0x0E` | 4 | `UINT32LE` | `header_size` | Size of the information header, always 40. | supported | FND-GFX-002 |
| `0x12` | 4 | `INT32LE` | `width` | 0 in every file. The executable replaces it with the width its caller gives. | supported | FND-GFX-002, FND-PLATFORM-002 |
| `0x16` | 4 | `INT32LE` | `height` | 0 in every file. The executable replaces it with the height its caller gives. | supported | FND-GFX-002, FND-PLATFORM-002 |
| `0x1A` | 2 | `UINT16LE` | `planes` | 255 in every file. The executable replaces it with 1. | supported | FND-GFX-002, FND-PLATFORM-002 |
| `0x1C` | 2 | `UINT16LE` | `bit_count` | Bits per pixel, always 8. | supported | FND-GFX-002, FND-PLATFORM-002 |
| `0x1E` | 4 | `UINT32LE` | `compression` | How the pixel data is stored, one of the values below. | supported | FND-GFX-002, FND-PLATFORM-002 |
| `0x22` | 4 | `UINT32LE` | `image_size` | Size of the pixel data in bytes, the file size minus 1,078. | supported | FND-GFX-002 |
| `0x26` | 4 | `INT32LE` | `x_pixels_per_meter` | 2,835. | supported | FND-GFX-002 |
| `0x2A` | 4 | `INT32LE` | `y_pixels_per_meter` | 2,835. | supported | FND-GFX-002 |
| `0x2E` | 4 | `UINT32LE` | `colors_used` | 0, which means the colour table has all 256 entries. | supported | FND-GFX-002 |
| `0x32` | 4 | `UINT32LE` | `colors_important` | 0. | supported | FND-GFX-002 |
| `0x36` | 1024 | `FMT-GFX-003[256]` | `palette` | The colour of each pixel value 0 to 255. | supported | FND-GFX-002 |
| `0x436` | `image_size if compression == 1` | `BYTE[image_size]` | `rle8_data` | Pixel data compressed as BMP RLE8. RULE-GFX-001 decodes it into `height` rows of `width` pixel values, bottom row first. | supported | FND-GFX-002, FND-GFX-003 |
| | `image_size if compression == 0` | `BYTE[image_size]` | `raw_pixels` | `height` rows of `width` one-byte pixel values, bottom row first, each row padded with zero bytes to a multiple of 4. The seven files that use it are 344 pixels wide, so their rows have no padding. | supported | FND-GFX-002, FND-GFX-003 |
| | | | | Total size 1078 + image_size | | |

## Enumerations and flags

### `compression`

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 0 | `BMP_COMPRESSION_NONE` | The pixel data is `raw_pixels`. Used by `PX05000`, `PX05001`, `PX05002`, `PX05004`, `PX05007`, `PX05018` and `Px05013`. | supported | FND-GFX-002 |
| 1 | `BMP_COMPRESSION_RLE8` | The pixel data is `rle8_data`. Used by the other 207 files. | supported | FND-GFX-002 |

## Differences between builds

None known.

## Coverage

All 214 files under `DATA/PX08/` of BLD-GOG-EN-1.1 were read with a script
(FND-GFX-002). Every header field holds the value in the table, and every
`rle8_data` block decodes with RULE-GFX-001 to lines of equal length that end
with the end-of-bitmap code on the file's last two bytes. The widths and
heights, from the line lengths and line counts, are those in the Coverage
table of FMT-GFX-001 for the same names, apart from `PX00131`, which has no
`PX08` file (FND-GFX-003). The executable passes the same sizes, except
`PX06008`, which it reads as 242 x 158 (FND-GFX-005). The Kaitai definition
compiles and parses all 214 files, with `rle8_data` kept as stored bytes
(FND-DATA-006).

## Open questions

- Which pixels `SetDIBits` leaves in the row and column of `PX06008` that the
  RLE8 data never sets has not been observed (FND-GFX-005).
