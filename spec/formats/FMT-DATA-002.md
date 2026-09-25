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
evidence: [FND-DATA-002, FND-ASSET-001, FND-GANG-001, FND-AI-008, FND-UPKEEP-001, SRC-RECHAOS-3561D41]
conflicting: []
split_with: []
related: []
---

## Layout

`DATA/Gangs` holds 90 of these records back to back, record `n` at offset
`n * 156` (FND-DATA-002). The executable opens it as `data\Gangs`
(FND-ASSET-001). Every number is a signed 16-bit little-endian integer.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 30 | `char[30]` | `name` | The gang's name. ASCII text, then one NUL byte, then spaces to the end of the field. Read it as a C string. | sourced | FND-DATA-002, SRC-RECHAOS-3561D41 |
| `0x1E` | 2 | `INT16LE` | `id` | The gang's number, equal to the record's index, 0 to 89. | supported | FND-DATA-002 |
| `0x20` | 90 | `char[90]` | `description` | The gang's description. ASCII text padded with spaces to the end of the field, with no NUL byte. | sourced | FND-DATA-002, SRC-RECHAOS-3561D41 |
| `0x7A` | 2 | `INT16LE` | `force` | The value SRC-RECHAOS-3561D41 calls the gang's Force. The computer players compare it with cash as the price of hiring the gang (FND-AI-008). | supported | FND-DATA-002, FND-AI-008 |
| `0x7C` | 2 | `INT16LE` | `upkeep` | Upkeep paid each turn for the gang. | supported | FND-DATA-002, FND-AI-008, FND-UPKEEP-001 |
| `0x7E` | 2 | `INT16LE` | `combat` | Base Combat. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x80` | 2 | `INT16LE` | `defense` | Base Defense. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x82` | 2 | `INT16LE` | `tech_level` | Tech Level. | sourced | SRC-RECHAOS-3561D41 |
| `0x84` | 2 | `INT16LE` | `stealth` | Base Stealth. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x86` | 2 | `INT16LE` | `detect` | Base Detect. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x88` | 2 | `INT16LE` | `chaos` | Base Chaos. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x8A` | 2 | `INT16LE` | `control` | Base Control. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x8C` | 2 | `INT16LE` | `heal` | Base Heal. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x8E` | 2 | `INT16LE` | `influence` | Base Influence. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x90` | 2 | `INT16LE` | `research` | Base Research. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x92` | 2 | `INT16LE` | `strength` | Base Strength. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x94` | 2 | `INT16LE` | `blade` | Base Blade. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x96` | 2 | `INT16LE` | `range` | Base Range. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x98` | 2 | `INT16LE` | `fighting` | Base Fighting. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x9A` | 2 | `INT16LE` | `martial_arts` | Base Martial Arts. | sourced | FND-GANG-001, SRC-RECHAOS-3561D41 |
| `0x9C` | | | | Total size 156 | | |

## Enumerations and flags

None.

## Differences between builds

None known.

## Coverage

`DATA/Gangs` of BLD-GOG-EN-1.1 (14,040 bytes) was read with a script as 90
records of this layout (FND-DATA-002). Every byte falls in a field, every `id`
equals its record's index, every name has the NUL-then-spaces form, and no
description holds a NUL. The Kaitai definition has not been compiled or run
against the file.

## Open questions

- FND-GANG-001 shows the executable building each gang's fourteen effective
  statistics from fields of this record, but not the offset of each field.
  The rows from `combat` to `martial_arts` rest on the source's names until
  those reads are listed.
- `tech_level` is not tied to any executable read.
- Whether `force` is the Force a hired gang starts with, or a maximum, is not
  settled by FND-AI-008, which only shows the hire ranking reading it.
- Where the executable keeps the loaded table in memory has not been written
  down.
