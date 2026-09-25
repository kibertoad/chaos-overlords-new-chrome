---
id: FMT-DATA-002
title: Gang definition records in DATA/Gangs
status: sourced
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: ["DATA/Gangs"]
byte_order: little
size: 156
text: false
definition: fmt_data_002.ksy
evidence: [FND-DATA-002, FND-DATA-006, FND-DATA-007, FND-GANG-007, FND-ASSET-001, FND-GANG-001, FND-AI-008, FND-UPKEEP-001, FND-HIRE-006, FND-HIRE-007, FND-HIRE-009, FND-RESEARCH-003, SRC-RECHAOS-3561D41]
conflicting: []
split_with: []
related: []
---

## Layout

`DATA/Gangs` holds 90 of these records back to back, record `n` at offset
`n * 156` (FND-DATA-002). The executable opens it as `data\Gangs`
(FND-ASSET-001) and keeps the records at `0x004A2800`, stride `0x9C`
(FND-HIRE-006). Every number is a signed 16-bit little-endian integer.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 30 | `char[30]` | `name` | The gang's name. ASCII text, then one NUL byte, then spaces to the end of the field. Read it as a C string. | sourced | FND-DATA-002, SRC-RECHAOS-3561D41 |
| `0x1E` | 2 | `INT16LE` | `id` | The gang's number, equal to the record's index, 0 to 89. | supported | FND-DATA-002 |
| `0x20` | 90 | `char[90]` | `description` | The gang's description. ASCII text padded with spaces to the end of the field, with no NUL byte. | sourced | FND-DATA-002, SRC-RECHAOS-3561D41 |
| `0x7A` | 2 | `INT16LE` | `hire_cost` | The price of hiring the gang. The hire resolution tests it against cash and charges it, a price of 0 skipping the cash test; the console draws it under each offer; the computer players compare it with cash. SRC-RECHAOS-3561D41 calls it the gang's Force. | supported | FND-DATA-002, FND-HIRE-006, FND-HIRE-007, FND-AI-008 |
| `0x7C` | 2 | `INT16LE` | `upkeep` | Upkeep paid each turn for the gang. | supported | FND-DATA-002, FND-AI-008, FND-UPKEEP-001 |
| `0x7E` | 2 | `INT16LE` | `combat` | Base Combat. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x80` | 2 | `INT16LE` | `defense` | Base Defense. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x82` | 2 | `INT16LE` | `tech_level` | Tech Level: the limit the Research list compares item Tech Levels with, and the first row of the hire comparison panel. Not copied into the gang record. | supported | FND-RESEARCH-003, FND-HIRE-009, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x84` | 2 | `INT16LE` | `stealth` | Base Stealth. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x86` | 2 | `INT16LE` | `detect` | Base Detect. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x88` | 2 | `INT16LE` | `chaos` | Base Chaos. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x8A` | 2 | `INT16LE` | `control` | Base Control. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x8C` | 2 | `INT16LE` | `heal` | Base Heal. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x8E` | 2 | `INT16LE` | `influence` | Base Influence. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x90` | 2 | `INT16LE` | `research` | Base Research. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x92` | 2 | `INT16LE` | `strength` | Base Strength. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x94` | 2 | `INT16LE` | `blade` | Base Blade. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x96` | 2 | `INT16LE` | `range` | Base Range. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x98` | 2 | `INT16LE` | `fighting` | Base Fighting. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x9A` | 2 | `INT16LE` | `martial_arts` | Base Martial Arts. | supported | FND-GANG-001, FND-HIRE-006, SRC-RECHAOS-3561D41 |
| `0x9C` | | | | Total size 156 | | |

## Enumerations and flags

None.

## Differences between builds

None known.

## Coverage

`DATA/Gangs` of BLD-GOG-EN-1.1 (14,040 bytes) was read with a script as 90
records of this layout (FND-DATA-002). Every byte falls in a field, every `id`
equals its record's index, every name has the NUL-then-spaces form, and no
description holds a NUL. The Kaitai definition compiles and parses the file
(FND-DATA-006). The executable loads the whole file into `0x004A2800` and
reads the fields at these offsets (FND-DATA-007); the statistics rebuild reads
`combat` to `martial_arts` in the pairing FND-GANG-007 gives.

## Open questions

- The rows from `combat` to `martial_arts` are placed by the hire
  resolution, which copies the low byte of each into the gang record field
  of the same statistic (FND-HIRE-006). What each statistic does is in the
  rules that read the gang record.
