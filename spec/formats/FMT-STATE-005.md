---
id: FMT-STATE-005
title: Comlink message record
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
files: []
byte_order: little
size: 166
text: false
definition: fmt_state_005.ksy
evidence: [FND-COMLINK-001, FND-COMLINK-004, FND-COMLINK-006, FND-COMLINK-008, FND-SAVE-001]
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
`occupied` 0 and `read` 1 [FND-COMLINK-006]; whether a load passes through it
has not been recorded.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `UINT8` | `occupied` | 1 when the record holds a message, 0 when it is empty | supported | FND-COMLINK-004, FND-COMLINK-006 |
| `0x01` | 1 | `UINT8` | `read` | 1 once the recipient has viewed the message; 0 in a new message and 1 in an empty record | supported | FND-COMLINK-004, FND-COMLINK-006 |
| `0x02` | 2 | `INT16LE` | `turn` | The zero-based turn the message was sent in: the low 16 bits of `elapsed_turns` when the Send panel opened. Loaded signed by View | supported | FND-COMLINK-004, FND-COMLINK-008 |
| `0x04` | 1 | `UINT8` | `sender` | The sending player slot | supported | FND-COMLINK-004, FND-COMLINK-008 |
| `0x05` | 160 | `char[160]` | `text` | The message, four rows of 40 characters shown one row per line. One byte per character, each from `0x20` (space) to `0x5A` (`Z`); lower-case letters are typed as capitals. Unused characters are spaces; there is no terminator | supported | FND-COMLINK-004, FND-COMLINK-008 |
| `0xA5` | 1 | `UINT8` | `unk_A5` | Spare. No code writes or reads it on its own; it is copied with the record and so carries the Send buffer's initial value, 0 | supported | FND-COMLINK-004, FND-COMLINK-008 |
| `0xA6` | | | | Total size 166 | | |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original.

## Open questions

None known.
