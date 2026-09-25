---
id: FND-STATE-006
title: In the computer players' 16-byte planning record, byte 1 is a flag only selector 0x48 reads, byte 11 is never referenced, and byte 15 is the high byte of the 16-bit field at 14
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048A250..0x0048C0AF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409DE1..0x00409F46
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0..0x004594C6
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00405A72
tool: Ghidra 12.1.3
environment: null
---

## Observation

The records are at `0x0048A250 + player * 0x510 + slot * 0x10` (FND-AI-019).
A reference search over each of the 16 byte offsets of the first record gives:

| Offset | References | Access |
|---|---|---|
| 0 | 90 | byte stores and `MOVSX` byte loads |
| 1 | 11 | byte stores of 0 or 1, and one byte load |
| 2, 3, 4 | 3 each | byte |
| 5 | 10 | byte |
| 6, 7 | 5 and 4 | byte |
| 8 | 200 | byte |
| 9 | 226 | byte |
| 10 | 46 | byte |
| 11 | none | |
| 12 | 20 | 16-bit word stores and `MOVSX` word loads |
| 13 | none | |
| 14 | 21 | 16-bit word stores and `MOVSX` word loads |
| 15 | none | |

- Offset 1: the reset `fn_00409DE1` stores 0 (`0x00409EFD`). The pre-planning
  pass `fn_00458FA0` stores 1 for slot 0 on a player's first plan
  (`0x00459036`) and for every slot whose gang record has sector 100
  (`0x0045930C`); seven family handlers store 1 (`0x0042093A`, `0x004327AB`,
  `0x00435387`, `0x00435BAF`, `0x00436C55`, `0x004384AD`, `0x0043B26F`). The
  only load is selector `0x48` of `fn_00402D70` (`0x00405A72`), which returns
  1 when the byte is nonzero and 0 otherwise.
- Offset 0: the reset stores 99 (`0x00409EDA`); `fn_00458FA0` stores 9 (`0x004594C6`) in
  every active record of a player whose byte at `0x00482158` is set.
- The reset also stores 0 in offsets 2 to 10 and in the words at 12 and 14.
- The block is saved and loaded whole (FND-SAVE-001, block 14, 7,776 bytes).

## Interpretation

The record is: family (byte 0), a flag (byte 1) that marks the record as
needing a fresh plan or as idle, three action triplets (2..4, 5..7, 8..10),
one unused byte (11), and two signed 16-bit values (12, 14). Bytes 13 and 15
are the high bytes of those words.

## Alternatives

- What the flag at offset 1 means to the planner is not settled: it is set by
  handlers and for empty slots and read by one selector, whose uses belong to
  the AI entries.
- Offset 11 could be reached through a whole-record copy; the save, the load
  and the network copy move the whole block, and no code was found that reads
  the byte on its own.

## How to reproduce

List the references to each address `0x0048A250..0x0048A25F`. Read
`0x00409DE1..0x00409F46` and the start of `0x00458FA0`, and case `0x48` of
`0x00402D70`.
