---
id: FMT-DATA-001
title: Site definition records in DATA/SITES
status: sourced
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/SITES"]
byte_order: little
size: 62
text: false
definition: fmt_data_001.ksy
evidence: [FND-DATA-001, FND-DATA-006, FND-DATA-007, FND-STATE-001, FND-ASSET-001, FND-GANG-001, FND-CITY-002, FND-EXE-004, SRC-RECHAOS-3561D41]
conflicting: []
split_with: []
related: []
---

## Layout

`DATA/SITES` holds 22 of these records back to back, record `n` at offset
`n * 62`, with nothing before or after them (FND-DATA-001). The executable
opens the file as `data\Sites` (FND-ASSET-001). Every number is a signed
16-bit little-endian integer.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 20 | `char[20]` | `name` | The site's name. ASCII text, then one NUL byte, then spaces to the end of the field. Read it as a C string. | sourced | FND-DATA-001, SRC-RECHAOS-3561D41 |
| `0x14` | 2 | `INT16LE` | `id` | The site's number, equal to the record's index, 0 to 21. The site information panel uses it as the row, 64 pixels each, of the site's picture. | supported | FND-DATA-001, FND-DATA-007 |
| `0x16` | 2 | `INT16LE` | `resistance` | Resistance: the Influence progress needed before the site counts as influenced. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x18` | 2 | `INT16LE` | `support` | Support the site adds to its sector once influenced. | supported | FND-DATA-007, FND-STATE-001, SRC-RECHAOS-3561D41 |
| `0x1A` | 2 | `INT16LE` | `frequency` | Named frequency by the source. No instruction in the executable reads it: city generation draws sites uniformly (FND-CITY-002), and neither the table's readers nor the site panel's copy of a record use this offset (FND-DATA-007). | supported | FND-DATA-007, FND-CITY-002, SRC-RECHAOS-3561D41 |
| `0x1C` | 2 | `INT16LE` | `tolerance` | Tolerance the site adds to its sector once influenced. | supported | FND-DATA-007, FND-STATE-001, SRC-RECHAOS-3561D41 |
| `0x1E` | 2 | `INT16LE` | `cash` | Cash the site adds to its sector's income once influenced. | supported | FND-DATA-007, FND-STATE-001, SRC-RECHAOS-3561D41 |
| `0x20` | 2 | `INT16LE` | `combat` | Modifier to Combat for the sector owner's gangs in the sector once the site is influenced. 0 in every record. | supported | FND-DATA-007, FND-STATE-001, FND-DATA-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x22` | 2 | `INT16LE` | `defense` | Modifier to Defense, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x24` | 2 | `INT16LE` | `stealth` | Modifier to Stealth, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x26` | 2 | `INT16LE` | `detect` | Modifier to Detect, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x28` | 2 | `INT16LE` | `chaos` | Modifier to Chaos, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x2A` | 2 | `INT16LE` | `control` | Modifier to Control, applied the same way as `combat`. 0 in every record. | supported | FND-DATA-007, FND-STATE-001, FND-DATA-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x2C` | 2 | `INT16LE` | `heal` | Modifier to Heal, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x2E` | 2 | `INT16LE` | `influence` | Modifier to Influence, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x30` | 2 | `INT16LE` | `research` | Modifier to Research, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x32` | 2 | `INT16LE` | `strength` | Modifier to Strength, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x34` | 2 | `INT16LE` | `blade` | Modifier to Blade, applied the same way as `combat`. 0 in every record. | supported | FND-DATA-007, FND-STATE-001, FND-DATA-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x36` | 2 | `INT16LE` | `range` | Modifier to Range, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x38` | 2 | `INT16LE` | `fighting` | Modifier to Fighting, applied the same way as `combat`. | supported | FND-DATA-007, FND-STATE-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x3A` | 2 | `INT16LE` | `martial_arts` | Modifier to Martial Arts, applied the same way as `combat`. 0 in every record. | supported | FND-DATA-007, FND-STATE-001, FND-DATA-001, FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x3C` | 2 | `INT16LE` | `special` | The site's special effect, one of the values below. 0 in 19 records, and 1, 2 and 3 in one record each. | supported | FND-DATA-007, FND-STATE-001, FND-DATA-001, SRC-RECHAOS-3561D41 |
| `0x3E` | | | | Total size 62 | | |

## Enumerations and flags

### `special`

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 0 | `SITE_SPECIAL_NONE` | No special effect. | sourced | FND-DATA-001, SRC-RECHAOS-3561D41 |
| 1 | `SITE_SPECIAL_RESEARCH_TECH_8` | Research effect that the source ties to Tech Level 8. | sourced | FND-DATA-001, SRC-RECHAOS-3561D41 |
| 2 | `SITE_SPECIAL_RESEARCH_TECH_10` | Research effect that the source ties to Tech Level 10. | sourced | FND-DATA-001, SRC-RECHAOS-3561D41 |
| 3 | `SITE_SPECIAL_DISCOUNT` | Lowers the price of equipment bought in the sector. | sourced | FND-DATA-001, SRC-RECHAOS-3561D41 |

## Differences between builds

None known.

## Coverage

`DATA/SITES` of BLD-GOG-EN-1.1 (1,364 bytes) was read with a script as 22
records of this layout (FND-DATA-001). Every byte falls in a field, every
`id` equals its record's index, every `special` value is in the table above,
and every name field has the NUL-then-spaces form. The Kaitai definition
compiles and parses the file (FND-DATA-006). The executable loads the whole
file into `0x004AB668` and reads the fields at these offsets (FND-DATA-007);
the refresh before planning adds Support, Tolerance, Cash and the fourteen
modifiers of each completed site to its sector and turns `special` into the
sector's research and discount bytes (FND-STATE-001).

## Open questions

- The effects of `SITE_SPECIAL_RESEARCH_TECH_8` and
  `SITE_SPECIAL_RESEARCH_TECH_10` are not pinned: the source's README gives
  value 1 to one research site and value 2 to the other, while its C header
  swaps the two sites' names. Neither says exactly what the effect does.
- Value 3 of `special` sets sector byte `0x0E` (FND-STATE-001); that this is
  the discount the Equip resolver tests (FND-EQUIP-001) has not been traced.
