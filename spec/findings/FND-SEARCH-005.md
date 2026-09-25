---
id: FND-SEARCH-005
title: The save file does not hold the Search filter table, and the match function clears it on entry whether it starts a new match or resumes a loaded one
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A24E8..0x004A256B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E79C..0x0046E7DC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046EB4E
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

- The Search filter table is the 132 bytes at `0x004A24E8..0x004A256B`
  (FND-SEARCH-001). Its nine references are in the Search handler
  `fn_00448E32`, the city map drawer `fn_004123CC` (`0x00412990`), the helper
  `fn_00449925`, and the clear at `0x0046E7DC`. Neither the load function
  `fn_0046381A` nor the save function `fn_00463CC5` references it.
- None of the 44 blocks the save and load functions move covers it
  (FND-SAVE-001). The nearest blocks are `0x004A11E8` (4,860 bytes, ending at
  `0x004A24E3`) and `0x004A2570` (24 bytes).
- The match function `fn_0046E766` clears all six players' 22 bytes in its
  opening loop (`0x0046E79C..0x0046E7DC`), before the test of its argument at
  `0x0046EB4E` that chooses between setting up a new match (argument nonzero)
  and carrying on with the state already in memory (argument 0). The shell
  `fn_00460CCF` calls it with 1 at `0x0046179A` and `0x004619E5`, and with 0 at
  `0x00461BBF`, `0x00461EB6`, `0x0046204A` and `0x00462212`; the shell's load
  (`0x00461902`, through `fn_004637B8`) happens outside the match function.

## Interpretation

The Search filters are not saved. A match resumed from a save through the
match function starts with every player's filter empty, the same as a new
match.

## Alternatives

- Which of the shell's calls with argument 0 follows a load, and which follows
  a network or hot-seat resumption, has not been traced call by call. All of
  them pass through the clear, so the result holds for each.
- The load function has a second caller, the window procedure `fn_0045C33B`
  (`0x0045C6C6`), which opens a save when the window is created. It is also
  outside the match function.

## How to reproduce

List the references to `0x004A24E8`. Compare the block list of `0x0046381A`
and `0x00463CC5` with the range `0x004A24E8..0x004A256B`. Read the opening of
`0x0046E766` up to `0x0046EB4E`, and the pushes before each call to it in
`0x00460CCF`.
