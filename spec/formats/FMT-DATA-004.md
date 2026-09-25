---
id: FMT-DATA-004
title: Colour list in DATA/CLT00002
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/CLT00002"]
byte_order: little
size: 944
text: false
definition: null
evidence: [FND-DATA-004, FND-PLATFORM-007, FND-PLATFORM-011, FND-DATA-006]
conflicting: []
split_with: []
related: []
---

## Layout

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 944 | `BYTE[944]` | `entries` | 236 entries of 4 bytes: red, green, blue, then a byte that is 4 in every entry and is not read. Each colour level is a multiple of 17. Entry `n` becomes entry `n + 10` of the 256-colour palette, between the 20 colours Windows reserves; the loader sets the flag `PC_NOCOLLAPSE` itself. | supported | FND-DATA-004, FND-PLATFORM-011 |
| `0x3B0` | | | | Total size 944 | | |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

`DATA/CLT00002` of BLD-GOG-EN-1.1 was read with a script (FND-DATA-004): 944
bytes, 236 entries, fourth byte 4 in every entry. The executable's only
palette loader opens this file, and only when the display runs at 8 bits per
pixel; it reads 1,024 bytes, and the 80 past the end of the file stay zero
and are not used (FND-PLATFORM-011). `PX08` images are drawn through their
own colour tables, which GDI maps to this palette. No Kaitai definition
exists; the file is a flat array.

## Open questions

- Which colours GDI picks when a `PX08` image's table differs from this
  palette has not been computed.
