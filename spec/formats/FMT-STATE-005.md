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
evidence: [FND-COMLINK-001, FND-COMLINK-004, FND-SAVE-001]
conflicting: []
split_with: []
related: []
---

## Layout

The game keeps 16 of these records for each player in the list
`comlink_messages`, at `0x0049CA90 + player * 0xA60 + index * 0xA6`
[FND-COMLINK-001, FND-COMLINK-004]. Each record is a complete copy of one
message as the View panel shows it. The list is not one of the blocks a save
file moves [FND-SAVE-001], so a saved match does not keep its messages; what
a load leaves in the list has not been recorded.

| Offset | Size | Type | Name | Meaning | Status | Evidence |
|---|---|---|---|---|---|---|
| `0x00` | 1 | `UINT8` | `occupied` | Nonzero when the record holds a message | supported | FND-COMLINK-004 |
| `0x01` | 1 | `UINT8` | `read` | Set when the recipient has viewed the message | supported | FND-COMLINK-004 |
| `0x02` | 2 | `INT16LE` | `turn` | The zero-based turn the message was sent in | supported | FND-COMLINK-004 |
| `0x04` | 1 | `UINT8` | `sender` | The sending player slot | supported | FND-COMLINK-004 |
| `0x05` | 160 | `char[160]` | `text` | The message, four rows of 40 characters shown one row per line. Encoding and padding not recorded | supported | FND-COMLINK-004 |
| `0xA5` | 1 | `UINT8` | `unk_A5` | Purpose unknown. Copied with the record; View never reads it | supported | FND-COMLINK-004 |
| `0xA6` | | | | Total size 166 | | |

## Enumerations and flags

None known.

## Differences between builds

None known.

## Coverage

A memory structure: nothing has been decoded against a dump of the running
original.

## Open questions

- `unk_A5`: purpose unknown; the Send path that writes the record has not been
  read for it.
- The encoding of `text` and what fills the unused characters.
- Whether `turn` is signed.
