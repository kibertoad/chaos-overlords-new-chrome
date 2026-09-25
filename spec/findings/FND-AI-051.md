---
id: FND-AI-051
title: The placement anchor is a 32-bit value tested for free land, then occupancy, then Big Man, and a failed anchor of 63 blocks hiring for player 0
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004594EC..0x00459567
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00403E17..0x00403F3E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048E2F8..0x0048E30F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A08C4..0x004A08CF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408214
tool: Ghidra 12.1.3
environment: null
---

## Observation

Every access to the placement anchor at `0x0048E2F8 + player * 4` is a 32-bit
load or store.

The refresh in `0x00458FA0` runs at `0x004594EC..0x00459567`, after the
duplicate-Chaos cleanup and before the gang limit. It passes the anchor minus
`0x40` to selector `0x24` (`0x00459502`); a result of 0 jumps to the
replacement (`0x0045950C`). Otherwise it compares the 32-bit value at
`0x00489850 + player * 0x100 + anchor * 4` with 5 (`0x00459522`), which is
element `player * 64 + anchor - 0x40` of `sector_gang_count`; above 5 jumps to
the replacement. Otherwise selector 0 (the scenario) equal to 8 leads to the
replacement (`0x00459540`), and any other value keeps the anchor. The
replacement calls selector `0x25` with the player and two zeros
(`0x00459553`), adds `0x40` and stores the result (`0x00459561`).

The selector-`0x24` case (`0x00403E17`) first compares the signed owner byte
at `0x004A08E8 + center * 0x24` with the player. It then visits the nine cells
`center + dy * 8 + dx` and counts a cell when it lies in 0 to 64, its owner
byte is -1 and its byte +0x0F is 0. The column test takes the signed remainder
of `center` by 8 and excludes `dx = -1` only when that remainder is 0, and
`dx = 1` only when it is 7.

For a centre of -1 the owner read is at `0x004A08C4`. No instruction refers
to any address from `0x004A08C4` to `0x004A08CF`, and the image holds zero
there; the next referenced value is the per-player 32-bit array at
`0x004A08D0`. The remainder of -1 by 8 is -1, so neither column exclusion
applies and the cells in range are 0, 6, 7 and 8.

For an anchor of 63 the occupancy load is at `0x0048994C + player * 0x100`.
For player 0 that is the 32-bit value just before `sector_gang_count`, which
no instruction refers to and which is zero in the image; for a player from 1
to 5 it is element 63 of the previous player's row.

The hire placement `0x00408214` decodes its placement argument by subtracting
`0x40`; for sector -1 it returns 99 and stores no hire order.

## Interpretation

The keep test is a short-circuit chain in the order free land, occupancy,
scenario. For an ordinary anchor the order does not change the result.

When every replacement pass fails, the anchor becomes 63. For player 0 the
owner read for sector -1 gives 0, which is the player, so `free_neighbours`
counts the free neutral cells among sectors 0, 6, 7 and 8; the occupancy read
gives 0. While one of those four sectors is neutral without a Crackdown and
the scenario is not Big Man, player 0 keeps anchor 63 without trying a
replacement, even after it has gained a sector the scan would accept, and
every hire placed at the anchor is dropped. For players 1 to 5 the owner read
gives 0, which is not the player, so the scan runs again at every refresh and
the anchor recovers as soon as a replacement exists. Player 0 is a computer
player only when the human does not sit in slot 0.

## Alternatives

A path that writes into `0x004A08C4..0x004A08CF` through an index into a
neighbouring array (for example the array at `0x004A08D0` with a negative
index) is not excluded; none was found among the references to that array.

## How to reproduce

In `0x00458FA0`, read the block from `0x004594EC` to the store at
`0x00459561`. In `0x00402D70`, read the selector-`0x24` case at `0x00403E17`
and its column tests. List the references to `0x004A0700..0x004A08E7` and to
`0x00489900..0x0048994F`.
