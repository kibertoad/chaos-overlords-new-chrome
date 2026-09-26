---
id: FND-GANG-003
title: Death and Terminate write only the gang record's sector byte, leaving its items and other fields in place
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476F3B
tool: Ghidra 12.1.3
environment: null
---

## Observation

- After the attacks and the police have added their damage against Force
  values copied at the start of the combat phase, the whole-turn resolver
  `fn_00472775` applies the damage in a loop over players and their 81 gang
  records. When the resulting Force is below 1, the branch writes only the
  sector byte (offset `0x02`, `0x00498DAA` for the first record) with 100 and
  adds one to the owning player's casualty count. The combat report copy made
  just before it records the Force and all three item bytes. The death branch
  writes nothing to the weapon (`0x04`), armor (`0x05`) or miscellaneous
  (`0x06`) byte.
- The Terminate pass copies the whole 32-byte record out, tests action 14,
  changes only the copy's sector byte to 100 and copies all eight 32-bit words
  back (FND-MOVE-001).
- The Eliminate scenario's clean-up helper `fn_00476F3B` also writes only the
  sector byte, with 100.
- A normal hire later copies a complete new gang record into an inactive slot.

## Interpretation

A dead or terminated gang's items stay in its inactive record, where nothing
can reach them: they are not returned to the player and cannot be picked up.
The manual's statement that Terminate removes the gang's items is true for play
but not a clearing of the record. The next hire into the slot overwrites them.

## Alternatives

Where the casualty count is kept, and its type, are not recorded.

## How to reproduce

In `fn_00472775`, find the loop after the police pass that subtracts the
accumulated damage from Force and compares the result with 1. Its store of 100
to offset 2 of the gang record and its increment of a per-player counter are
the only writes in that branch. `fn_00476F3B` is reached from the Eliminate
scenario's end-of-turn handling.
