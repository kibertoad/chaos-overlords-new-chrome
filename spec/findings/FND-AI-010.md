---
id: FND-AI-010
title: The AI keeps an encoded hire placement anchor per player and refreshes it by fixed scans
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048E2F8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A11E8..0x004A11F8
tool: Ghidra 12.1.3
environment: null
---

## Observation

The outer planner `0x00458FA0` keeps one encoded placement anchor per player at
`0x0048E2F8`. New-match initialization sets it to the Right Hands' sector plus
`0x40`. At each planning pass the planner keeps the proposed anchor only while
the anchor sector is owned by the player, has at least one neutral available
neighbour (selector `0x24` above 0), holds at most five of the player's gangs,
and the scenario is not 8. Otherwise selector `0x25` picks a replacement, which
is stored plus `0x40`. The anchor is saved and loaded with the game. The
selector-`0x24` check is at `0x00459502` and the selector-`0x25` fallback at
`0x00459553`, both after the duplicate-Chaos cleanup (FND-AI-019).

Selector `0x24(player, center)` of `0x00402D70` returns 0 unless the player
owns `center`. Otherwise it counts, in its 3-by-3 neighbourhood, visited row by
row and column by column within a row, the cells that are neutral and whose
Crackdown byte is 0. Its bounds check on a candidate is the linear test
`0 <= candidate < 65`, together with a check against wrapping past a row.

Index 64 is therefore read. It lies past the 64 sector records of 36 bytes at
`0x004A08E8`: its owner byte is `0x004A11E8` and its Crackdown byte
`0x004A11F7`, both inside the next block, the 486 ten-byte records at
`0x004A11E8`. `0x004A11E8` is byte 0 of the first record. According to a
bounded decompilation of the resolver `0x00472775`, with no instruction
address recorded, that byte holds the owner (player 0) of player 0's roster
slot 0. `0x004A11F7` is byte 5 of the second record, the retaliation damage
taken by player 0's roster slot 1.

For scenarios other than 8, selector `0x25` makes up to three passes over the
player's owned sectors that hold fewer than six of its gangs, in ascending
sector order:

1. the first sector with the strictly greatest positive selector-`0x24` count
   (the best starts at 0, so a count of 0 never qualifies, and an equal count
   keeps the earlier sector);
2. if none, the first sector whose selector-`0x5B` count (gangs whose previous
   action is Chaos) is 0;
3. if none, the first sector with the strictly smallest nonzero selector-`0x26`
   count, starting from a best of 9. Selector `0x26` counts the cells not owned
   by the player in the same 3-by-3 bounds, including index 64, and does not
   test availability.

If all three fail it returns -1. In scenario 8 it tests an empty list, then the
sectors `[18, 26, 19, 27]`, then
`[9, 17, 25, 33, 10, 18, 26, 34, 11, 19, 27, 35, 12, 20, 28, 36]`, taking the
first owned sector with fewer than six of the player's gangs. If none qualifies
it returns the incoming anchor minus 64, even when that sector is full.
Selector `0x25` makes no random draw.

The failure value -1 is stored as anchor 63. A later refresh subtracts 64 and
passes -1 to selector `0x24`, whose owner read happens before its bounds checks
and so reads the byte before the sector list. In scenario 8 the outer
re-encoding keeps 63, an ordinary encoded sector, or 164 (a sector of 100)
unchanged.

## Interpretation

Each computer player hires into one remembered sector. It keeps that sector
while it is a good base for expansion (owned, next to free land, not full) and
otherwise picks the owned sector with the most free land around it, then one
where it is not already running Chaos, then the one with the fewest foreign
neighbours. In Big Man it picks the most central owned sector.

## Alternatives

The owner reading of byte 0 of the first ten-byte record rests on a
decompilation without an instruction address. FND-COMBAT-004 reads byte 0 of
every such record as the gang's definition. For this record (player 0's Right
Hands, whose definition is 0) both readings give 0, so index 64 looks owned by
player 0 either way: it is never neutral for selector `0x24`, and so its
Crackdown alias is not consulted, and it counts as foreign for selector `0x26`
for every player but player 0. What the byte holds before the first combat
phase has written it, and whether any path writes -1 there, is not recorded.

## How to reproduce

Find the stores to `0x0048E2F8` in `0x00458FA0` and in the new-match
initializer `0x0046DC10` (Right Hands sector plus `0x40`). The calls with
selectors `0x24` and `0x25` are at `0x00459502` and `0x00459553`. In
`0x00402D70`, the selector-`0x24` case compares a candidate with 65; the Big
Man lists are constant arrays read by the selector-`0x25` case.
