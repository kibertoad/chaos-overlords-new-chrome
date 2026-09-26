---
id: FND-AI-003
title: The outer AI planning pass rolls action history, runs the dispatcher per gang, then picks a hire role
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040AB20
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409DE1
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x00458FA0` is the only normal caller of the per-gang dispatcher
`0x00432DA0`. It is called from `0x0040AB20` at `0x0040ABAC` and from
`0x0046E766` at `0x0046F4EC`.

The first time it runs for a player it calls `0x00409DE1` for all 81 roster
slots and initializes per-player state. On later passes it visits the 81
planning records (player stride `0x510`, record stride `0x10`), skipping any
whose gang record byte at `0x00498DAA` (the gang's sector, stride `0x20`) is
100. For each other record it copies bytes +5..+7 into +2..+4, clears +8..+10,
and updates the two 16-bit fields at +12 and +14 (FND-AI-019 gives the
update). It then visits the same 81 records again, sets family 9 under a
separate condition that has not been recorded, and calls `0x00432DA0` for
every record whose gang sector is not 100. Before the dispatch it runs a pass
over 64 sector-sized entries.

After the per-gang pass it switches on the scenario (0 to 9). Each branch
starts from a schedule slot chosen from the current turn, adjusts the slot
against the counts of existing families, calls `0x004078D9` with an offer
ranking mode, and writes the chosen hire role to `0x00482128 + player * 4`.

## Interpretation

This is the computer player's planning entry. The three three-byte groups in
each planning record are the older, the previous and the newly planned action
with its two target bytes, and a sector of 100 marks an empty roster slot. The
scenario switch after the dispatch is the hiring strategy (FND-AI-008,
FND-AI-009). The 64-entry pass prepares the per-sector values the handlers read
(FND-AI-018, FND-AI-040).

## Alternatives

The condition under which family 9 is set is not recorded. The 64-entry pass is
tied to the strategic refresh `0x0040A1A7` only through its effects (FND-AI-018,
FND-AI-040); FND-AI-019 gives the address of the refresh call.

## How to reproduce

Find the call at `0x004594DF` to `0x00432DA0`; its enclosing function is
`0x00458FA0`. Its callers are found through the references to `0x00458FA0`.
The rollover copies use the `0x510` and `0x10` strides from `0x0048A250`.
