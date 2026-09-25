---
id: FMT-STATE-004
title: Site slot in a sector record
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 2
text: false
definition: fmt_state_004.ksy
evidence: [FND-CITY-003, FND-CONTROL-001, FND-GANG-001, FND-TURN-001, FND-TURN-003, FND-TURN-004]
conflicting: []
split_with: []
related: []
---

## Layout

Each sector record (FMT-STATE-002) holds three of these at `0x07`, `0x09` and
`0x0B` [FND-TURN-001]. The game keeps no other per-site state: there is no
field naming the player who influenced a site. A completed site benefits the
sector's owner [FND-TURN-001, FND-GANG-001].

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `INT8` | `definition` | The site's record number in the site definition table read from `DATA/SITES`. City generation writes 21 into slot 0 of each headquarters sector | supported | FND-CITY-003, FND-TURN-001 |
| `0x01` | 1 | `INT8` | `progress` | Influence progress. The site is complete when it equals the Resistance of the site's definition. Set to 0 in all three slots whenever the sector's owner changes | supported | FND-CONTROL-001, FND-GANG-001, FND-TURN-001, FND-TURN-003, FND-TURN-004 |
| `0x02` | | | | Total size 2 | | |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original.

## Open questions

- Whether the game loads `definition` and `progress` as signed bytes is not
  recorded.
- The file name of the site definition table is taken from the format entries
  for the data files; the findings that place `definition` do not name the
  file.
