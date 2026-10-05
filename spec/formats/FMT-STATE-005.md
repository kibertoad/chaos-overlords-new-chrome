---
id: FMT-STATE-005
title: Comlink message record
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 166
text: false
definition: fmt_state_005.ksy
evidence: [FND-COMLINK-001, FND-COMLINK-004, FND-COMLINK-006, FND-COMLINK-008, FND-SAVE-001, FND-SEARCH-005, EXP-COMLINK-001]
conflicting: []
split_with: []
related: []
---

## Layout

The game keeps 16 of these records for each player in the list
`comlink_messages`, at `0x0049CA90 + player * 0xA60 + index * 0xA6`
[FND-COMLINK-001, FND-COMLINK-004]. Each record is a complete copy of one
message as the View panel shows it. The list is not one of the blocks a save
file moves [FND-SAVE-001], so a saved match does not keep its messages. The
outer match function empties every inbox when it starts, giving each record
`occupied` 0 and `read` 1 [FND-COMLINK-006]. Every load enters that function
again, so a loaded match starts with every inbox empty [FND-SEARCH-005].

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `UINT8` | `occupied` | 1 when the record holds a message, 0 when it is empty | established | FND-COMLINK-004, FND-COMLINK-006, EXP-COMLINK-001 |
| `0x01` | 1 | `UINT8` | `read` | 1 once the recipient has viewed the message; 0 in a new message and 1 in an empty record | established | FND-COMLINK-004, FND-COMLINK-006, EXP-COMLINK-001 |
| `0x02` | 2 | `INT16LE` | `turn` | The zero-based turn the message was sent in: the low 16 bits of `elapsed_turns` when the Send panel opened. Loaded signed by View | established | FND-COMLINK-004, FND-COMLINK-008, EXP-COMLINK-001 |
| `0x04` | 1 | `UINT8` | `sender` | The sending player slot | established | FND-COMLINK-004, FND-COMLINK-008, EXP-COMLINK-001 |
| `0x05` | 160 | `char[160]` | `text` | The message, four rows of 40 characters shown one row per line. One byte per character, each from `0x20` (space) to `0x5A` (`Z`); lower-case letters are typed as capitals. Unused characters are spaces; there is no terminator | established | FND-COMLINK-004, FND-COMLINK-008, EXP-COMLINK-001 |
| `0xA5` | 1 | `UINT8` | `unk_A5` | Spare. No code writes or reads it on its own; it is copied with the record and so carries the Send buffer's initial value, 0 | established | FND-COMLINK-004, FND-COMLINK-008, EXP-COMLINK-001 |
| `0xA6` | | | | Total size 166 | | |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

A memory structure. EXP-COMLINK-001 decoded every human player's 16 records
from the running original after each step of a three-player script: the empty
records of a new match, records stored by Send, by a full inbox and after the
drop at the end of planning, and records marked read by View. Every field
held what the layout gives, and `unk_A5` was 0 in every record.

## Open questions

None known.
