---
id: FND-OBJECTIVE-006
title: The network session transfer, the save and the load copy the scenario, the time limit and the turn counter as the same four-byte values, so a restored match keeps the scenario meanings
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046A115..0x0046A7CA
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046981D..0x0046A114
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449DD3..0x00449E25
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449E26..0x00449E78
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046381A..0x00463CC4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463CC5..0x00464107
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x0046A115(connection, block)` is called only from the transfer path
`0x0040D72F` (`0x0040D986`). It clears `0x004988F0` and switches on `block`.
Block 0 receives four bytes into the scenario dword `0x004ABBE8` and passes it
through `0x00449E26`, then does the same for `0x004A5EF8` and `0x0049CA68`;
between them it receives six bytes into the portrait table `0x004A5F00`, and
after them 24 bytes into `0x004ABBF0`, each dword passed through
`0x00449E26`. It has no other write to these globals and no table or
arithmetic between the received value and the store. Block 8 receives the cash,
the research table, the standings, the scores, the active bytes and the byte
`0x004ABBD4` in the same way.

`0x00449E26` and `0x00449DD3` each reverse the order of the four bytes of a
dword. The sender `0x0046981D`, called only from the packet dispatcher
`0x0046BA84` (`0x0046CBC8`), sends block 0 as the scenario, `0x004A5EF8`, the
six portrait bytes, `0x0049CA68` and the six dwords at `0x004ABBF0`, each dword
passed through `0x00449DD3` first, in the order the receiver reads them.

The save writer `0x0046381A` and the loader `0x00463CC5` pass the same three
globals, `0x0049CA68`, `0x004ABBE8` and `0x004A5EF8`, in that order, each with
length 4 and type 3, to the block writer `0x0042AE85` and the block reader
`0x0042AF31`.

## Interpretation

A match restored from the host or from a save carries the scenario value
unchanged, so it keeps the numbering of FND-OBJECTIVE-003: 0 Greed to 9
Armageddon. The byte swaps on both sides of the network transfer cancel out.
`turn_limit` and `elapsed_turns` travel the same way, so a restored timed match
ends at the same turn as the original match would.

## Alternatives

What type 3 means to the block writer and reader is not followed here; the
same type is used for all three dwords on both sides.

## How to reproduce

In Ghidra, open `0x0046A115` and read case 0 from `0x0046A142` to
`0x0046A1FE`. Open `0x0046981D` case 0 at `0x0046984B`, and `0x00449DD3` and
`0x00449E26`. List the references to `0x004ABBE8` and read the pushes at
`0x004638DF` and `0x00463DE4`.
