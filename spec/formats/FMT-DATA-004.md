---
id: FMT-DATA-004
title: Colour list in DATA/CLT00002
status: unknown
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/CLT00002"]
byte_order: little
size: 944
text: false
definition: null
evidence: [FND-DATA-004, FND-PLATFORM-007]
conflicting: []
split_with: []
related: []
---

## Layout

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 944 | `BYTE[944]` | `entries` | 236 entries of 4 bytes. In each, the first three bytes are colour levels that are multiples of 17 and the fourth byte is 4 (FND-DATA-004). Read as Windows `PALETTEENTRY` values (red, green, blue, flags `PC_NOCOLLAPSE`), they would fill entries 10 to 245 of the palette the loader builds (FND-PLATFORM-007). | unknown | FND-DATA-004, FND-PLATFORM-007 |
| `0x3B0` | | | | Total size 944 | | |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

`DATA/CLT00002` of BLD-GOG-EN-1.1 was read with a script (FND-DATA-004): 944
bytes, 236 entries, fourth byte 4 in every entry. No definition exists yet.

## Open questions

- Whether the palette loader `0x004282AA` opens this file (its template is
  `data\CLT00000`, and the number it writes in has not been traced).
- Whether the first byte of an entry is red or blue.
- Where the resulting palette is used: the `PX08` images carry palettes of
  their own in another order (FND-DATA-004).
