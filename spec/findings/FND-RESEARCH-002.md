---
id: FND-RESEARCH-002
title: New-game setup copies each item's research difficulty to every player, or zero for every item in Armageddon
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046DC10
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2608..0x004A2788
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A5F84
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABBE8..0x004ABBEC
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The fresh-game initializer `0x0046DC10` makes the only setup writes to the
  384-byte array at `0x004A2608`, element `item * 6 + player`.
- When the scenario dword at `0x004ABBE8` is not 9, it loops over all 64 item
  records and, inside, all six player slots, and copies the byte at
  `0x004A5F84 + item * 0xA6` into each element. The item records are 166
  (`0xA6`) bytes; that byte is the low byte of the 16-bit research-difficulty
  field at offset `0x7C` of the record.
- When the scenario is 9 (Armageddon), it writes 0 to all 384 elements
  instead.
- Neither branch draws a random number.
- The resolver's Research case and the item pickers read the same
  `item * 6 + player` elements.

## Interpretation

An element of 0 means the item is researched. An ordinary match starts with
exactly the items whose research difficulty is 0 already researched, and
Armageddon starts with every item researched. The loop covers all 64 records,
including the padding records of the item table, which are set in the same way.

## Alternatives

None known.

## How to reproduce

Find the writes to `0x004A2608` in `0x0046DC10`; the branch before them
compares `0x004ABBE8` with 9. The source address `0x004A5F84` with stride
`0xA6` is the research-difficulty field of the loaded item table.
