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
evidence: [FND-GFX-002]
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
Kaitai definition has not been compiled or run against the files.

## Open questions

- Whether the executable uses these colours at all, or only the palette it
  builds from a CLT file (FND-PLATFORM-007), has not been traced. The loader
  reads the colour table (FND-PLATFORM-002), but what it does with it is not
  written down.
