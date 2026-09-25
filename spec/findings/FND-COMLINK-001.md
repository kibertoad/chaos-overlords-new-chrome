---
id: FND-COMLINK-001
title: Each player keeps at most 16 Comlink messages, and a 17th drops the oldest
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D2F0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0049CA90..0x004A08D0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004981E0..0x004981F8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004981C8..0x004981E0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046BA84
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045EAB1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045FDF1
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The recorder `fn_0045D2F0` stores one 166-byte record in a table at
  `0x0049CA90` with a stride of `0xA60` bytes per player.
- It compares the player's count, at `0x004981E0 + player * 4`, with `0x10`.
  Below 16 it writes the record at the index given by the count and adds 1 to
  the count.
- At 16 it sets the index to 15, copies records 1 to 15 down into slots 0 to
  14, writes the new record into slot 15 and adds 1 to the count, which brings
  it back to 16. In the same branch it subtracts 1 from the player's message
  cursor at `0x004981C8 + player * 4` when that cursor is above 0, and leaves
  it at 0 otherwise.
- The recorder has four direct callers. Two are in the network dispatcher
  `fn_0046BA84`, handling packet type 10; they pass message ID 0 or the ID
  received. Two are in the Send handler `fn_0045EAB1`; they pass -1, which
  makes the recorder copy the message already composed in the global message
  buffer. The Send handler calls the recorder once for each recipient
  selected. For a recipient on another computer the recorder sends packet
  type 10.
- The Send handler loads `PX05018`, or resource 5023 as an alternative. Its
  helper `fn_0045FDF1` draws six recipient cells and four 40-character message
  rows from offsets in the same 166-byte buffer.

## Interpretation

Each player keeps the newest 16 messages. A new message beyond that drops the
oldest one, and the cursor moves with the remaining messages so that the
player stays on the same message. None of this touches the Last Turn report
table.

## Alternatives

None known.

## How to reproduce

Open `fn_0045D2F0` and find the comparison of the count at `0x004981E0` with
`0x10`, the copy loop of 15 records of `0xA6` bytes, and the cursor test at
`0x004981C8`. List its callers to reach `fn_0046BA84` and `fn_0045EAB1`.
