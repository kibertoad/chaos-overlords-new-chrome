---
id: FMT-STATE-006
title: Last Turn report record
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 10
text: false
definition: fmt_state_006.ksy
evidence: [FND-EVENT-001, FND-EVENT-004, FND-EVENT-005, FND-SAVE-001]
conflicting: []
split_with: []
related: [RULE-EVENT-002, RULE-EVENT-005]
---

## Layout

The game keeps 32 of these records for each player in the list
`last_turn_reports`, at `0x004AAE08 + player * 0x140 + index * 10`
[FND-EVENT-001, FND-EVENT-004]. The number of records a player holds is the
32-bit count at `0x004ABCA8 + player * 4` (`last_turn_report_count`)
[FND-EVENT-004]. The 1,920-byte table is block 24 of a save file; the counts
are not saved [FND-SAVE-001].

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `UINT8` | `occupied` | 1 when the record holds a report of the last resolution, 0 after the clearing before each resolution | supported | FND-EVENT-004 |
| `0x01` | 1 | `UINT8` | `unk_01` | Padding. Neither the recorder nor the clearing writes it | supported | FND-EVENT-004 |
| `0x02` | 2 | `INT16LE` | `report_type` | The kind of report, 0 to 9 (Enumerations) | supported | FND-EVENT-001, FND-EVENT-004 |
| `0x04` | 2 | `INT16LE` | `arg1` | First argument; its meaning depends on `report_type` (Enumerations) | supported | FND-EVENT-004, FND-EVENT-005 |
| `0x06` | 2 | `INT16LE` | `arg2` | Second argument | supported | FND-EVENT-004, FND-EVENT-005 |
| `0x08` | 2 | `INT16LE` | `arg3` | Third argument | supported | FND-EVENT-004, FND-EVENT-005 |
| `0x0A` | | | | Total size 10 | | |

## Enumerations and flags

### report_type

The arguments each type carries, as the resolver stores them and the panel
reads them. An argument not listed is stored as 0 and never read.

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 0 | `REPORT_NONE` | Never recorded. `arg1` to `arg3` are not read | supported | FND-EVENT-001, FND-EVENT-005 |
| 1 | `REPORT_CRACKDOWN` | A Crackdown in sector `arg1` | supported | FND-EVENT-004, FND-EVENT-005 |
| 2 | `REPORT_CONTROL_GAINED` | The recipient took sector `arg1` from player `arg2`, -1 when it had no owner. `arg2` is not read by the panel | supported | FND-EVENT-004, FND-EVENT-005 |
| 3 | `REPORT_CONTROL_LOST` | The recipient lost sector `arg1`: to player `arg2` through Control, or with `arg2` 0 through a third Crackdown. `arg2` is not read by the panel | supported | FND-EVENT-004, FND-EVENT-005 |
| 4 | `REPORT_SITE_COMPLETED` | Influence completed the site in slot `arg2` of sector `arg1` | supported | FND-EVENT-004, FND-EVENT-005 |
| 5 | `REPORT_RESEARCH_COMPLETED` | Research completed item `arg1` | supported | FND-EVENT-004, FND-EVENT-005 |
| 6 | `REPORT_CASH_SHORT` | An order failed for lack of cash; `arg1` tells which (below) | supported | FND-EVENT-004, FND-EVENT-005 |
| 7 | `REPORT_HIRE_SECTOR_FULL` | A hire into sector `arg1` failed because the player already has 6 gangs there | supported | FND-EVENT-004, FND-EVENT-005 |
| 8 | `REPORT_HIRE_ROSTER_FULL` | A hire of gang definition `arg1` failed for want of a free roster slot | supported | FND-EVENT-004, FND-EVENT-005 |
| 9 | `REPORT_ELIMINATION` | Player `arg1` was eliminated | supported | FND-EVENT-004, FND-EVENT-005 |

### arg1

Only for `REPORT_CASH_SHORT`; for other types `arg1` holds the value the
`report_type` table gives.

| Value | Name | Meaning | Status | Evidence |
|---|---|---|---|---|
| 1 | `CASH_SHORT_BRIBE` | A Bribe by a gang in sector `arg2` | supported | FND-EVENT-004, FND-EVENT-005 |
| 2 | `CASH_SHORT_EQUIP` | An Equip by a gang in sector `arg2`; `arg3` is the gang's byte at FMT-STATE-001 offset `0x01` (`definition`) | supported | FND-EVENT-004, FND-EVENT-005 |
| 4 | `CASH_SHORT_HIRE` | A hire of gang definition `arg2` | supported | FND-EVENT-004, FND-EVENT-005 |

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original or against the matching block of a save file.

## Open questions

- `unk_01` holds whatever the running game or a loaded save left there; no
  code reads it.
- Whether the network resolution paths `fn_0046A7CB` and `fn_0040CED0` write
  records was not read.
