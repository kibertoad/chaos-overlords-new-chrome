---
id: FND-SEARCH-001
title: Each player has 22 Search filter bytes, one per site definition, cleared when a new game starts
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00448E32
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A24E8..0x004A256C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044C476
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The Search handler `fn_00448E32` loads `PX05024` and the 220-by-56 sheet
  `PX00150`.
- The row states are 22 bytes per player at `0x004A24E8 + player * 22`, one
  per site definition.
- ALL and NONE write all 22 bytes of the active player. A click on a row flips
  that row's byte. A double-click on a row calls the Site Information handler
  `fn_0044C476` with the row's site definition.
- The new-game initialisation in `fn_0046E766` clears the whole table.

## Interpretation

Search keeps a filter of site definitions for each player. It changes what
the city draws (FND-SEARCH-003) and nothing else. The double-click shows the
definition's information, so a Resistance shown there is the definition's
value and not a live site's progress.

## Alternatives

- Whether the table is kept in the save file is not recorded.

## How to reproduce

Open `fn_00448E32` from its resource-5024 load. Read the loops that write 22
bytes from `0x004A24E8 + player * 22` for ALL and NONE, the single-byte flip,
and the double-click call to `fn_0044C476`. Search `fn_0046E766` for the
clearing of `0x004A24E8`.
