---
id: FMT-GFX-003
title: Palette entry in a PX08 image file
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/PX08/*"]
byte_order: little
size: 4
text: false
definition: fmt_gfx_003.ksy
evidence: [FND-GFX-002, FND-PLATFORM-011, FND-DATA-006]
conflicting: []
split_with: []
related: []
---

## Layout

One entry of the 256-entry colour table of a `PX08` file (FMT-GFX-002), which
starts at file offset `0x36`. Entry `n` gives the colour of pixel value `n`.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `UINT8` | `blue` | Blue level, 0 to 255. | supported | FND-GFX-002 |
| `0x01` | 1 | `UINT8` | `green` | Green level, 0 to 255. | supported | FND-GFX-002 |
| `0x02` | 1 | `UINT8` | `red` | Red level, 0 to 255. | supported | FND-GFX-002 |
| `0x03` | 1 | `UINT8` | `reserved_03` | 0 in every entry of every file. | supported | FND-GFX-002 |
| `0x04` | | | | Total size 4 | | |

## Enumerations and flags

None.

## Differences between builds

None known.

## Coverage

The 256 entries of all 214 files under `DATA/PX08/` of BLD-GOG-EN-1.1 were
read with a script (FND-GFX-002); `reserved_03` is 0 in all of them. The
Kaitai definition compiles and parses them (FND-DATA-006). The image loader
passes the colour table to `SetDIBits` with `DIB_RGB_COLORS`, so at 8 bits
GDI matches these colours to the palette built from `DATA/CLT00002`
(FND-PLATFORM-011).

## Open questions

- Which palette entry GDI picks for each colour has not been computed.
